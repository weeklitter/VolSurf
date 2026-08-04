using VolSurf.Core.Models.Dto;
using VolSurf.Data.Repositories;

namespace VolSurf.Core.Services;

/// <summary>
/// 波动率曲面组装服务：3D 曲面 / 微笑曲线 / 期限结构。
/// </summary>
public class VolSurfaceService(IOptionRepository optRepo)
{
    /// <summary>3D 曲面数据</summary>
    public async Task<VolSurfaceDto> GetVolSurfaceAsync(string underlying, DateTime date)
    {
        var tradeDate = date.Date;

        // 1. 当日 IV 数据
        var ivData = await optRepo.GetIvGreeksAsync(tradeDate, underlying);

        // 2. 标的价格
        var ulDaily = await optRepo.GetLatestUnderlyingDailyAsync(underlying);
        double underlyingPrice = ulDaily != null ? (double)ulDaily.Close : 0;

        // 3. 合约信息
        var contracts = await optRepo.GetActiveContractsAsync(underlying, tradeDate);
        var contractMap = contracts.ToDictionary(c => c.TsCode);

        // 4. 过滤：必须有 IV 且非异常 + 有合约信息
        var validData = ivData
            .Where(x => x.Iv.HasValue && !x.IvAnomaly && x.IvConfidence)
            .Where(x => contractMap.ContainsKey(x.TsCode))
            .ToList();

        // 5. 组装点
        var points = validData.Select(c =>
        {
            var contract = contractMap[c.TsCode];
            double K = (double)contract.ExercisePrice;
            return new SurfacePoint
            {
                Moneyness = K == 0 ? 0 : underlyingPrice / K,  // S/K
                TimeToExpiry = (contract.MaturityDate.Date - tradeDate).TotalDays / 365.0,
                Iv = (double)c.Iv!.Value,
                Strike = K,
                Expiry = contract.MaturityDate,
                CallPut = contract.CallPut
            };
        }).ToList();

        var expiries = points
            .Select(p => p.Expiry)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return new VolSurfaceDto
        {
            Underlying = underlying,
            Date = tradeDate.ToString("yyyy-MM-dd"),
            UnderlyingPrice = (decimal)underlyingPrice,
            Expiries = expiries.Select(d => d.ToString("yyyy-MM-dd")).ToList(),
            Points = points
        };
    }

    /// <summary>微笑曲线（单到期月）</summary>
    public async Task<VolSmileDto> GetVolSmileAsync(string underlying, DateTime date, DateTime expiry)
    {
        var tradeDate = date.Date;
        var expiryDate = expiry.Date;

        var ivData = await optRepo.GetIvGreeksAsync(tradeDate, underlying);
        var contracts = await optRepo.GetContractsByExpiryAsync(underlying, expiryDate);

        // 分认购 / 认沽
        var callData = (from c in contracts
                        join iv in ivData on c.TsCode equals iv.TsCode
                        where c.CallPut == "C" && iv.Iv.HasValue && iv.IvConfidence
                        select new
                        {
                            c.ExercisePrice,
                            Iv = (double)iv.Iv!.Value,
                            Delta = iv.Delta.HasValue ? (double)iv.Delta.Value : 0.0
                        }).ToList();

        var putData = (from c in contracts
                       join iv in ivData on c.TsCode equals iv.TsCode
                       where c.CallPut == "P" && iv.Iv.HasValue && iv.IvConfidence
                       select new
                       {
                           c.ExercisePrice,
                           Iv = (double)iv.Iv!.Value,
                           Delta = iv.Delta.HasValue ? (double)iv.Delta.Value : 0.0
                       }).ToList();

        var underlyingDaily = await optRepo.GetLatestUnderlyingDailyAsync(underlying);
        double S = underlyingDaily != null ? (double)underlyingDaily.Close : 0;

        var allData = callData.Concat(putData).ToList();
        if (allData.Count == 0)
        {
            return new VolSmileDto
            {
                Underlying = underlying,
                Expiry = expiryDate.ToString("yyyy-MM-dd"),
                Date = tradeDate.ToString("yyyy-MM-dd"),
                AtmIv = 0,
                Skew25 = 0,
                Calls = new(),
                Puts = new()
            };
        }

        // ATM IV：moneyness 最接近 1.0
        double atmIv = allData
            .OrderBy(x => Math.Abs(S / (double)x.ExercisePrice - 1.0))
            .First().Iv;

        // 25-delta skew：Delta ≈ -0.25 的 Put vs Delta ≈ +0.75 的 Call
        var put25Iv = putData.Count > 0
            ? putData.OrderBy(x => Math.Abs(x.Delta + 0.25)).First().Iv
            : atmIv;
        var call25Iv = callData.Count > 0
            ? callData.OrderBy(x => Math.Abs(x.Delta - 0.75)).First().Iv
            : atmIv;
        double skew25 = put25Iv - call25Iv;

        return new VolSmileDto
        {
            Underlying = underlying,
            Expiry = expiryDate.ToString("yyyy-MM-dd"),
            Date = tradeDate.ToString("yyyy-MM-dd"),
            AtmIv = atmIv,
            Skew25 = skew25,
            Calls = callData
                .Select(x => new SmilePoint
                {
                    Strike = (double)x.ExercisePrice,
                    Iv = x.Iv,
                    Delta = x.Delta
                })
                .OrderBy(x => x.Strike)
                .ToList(),
            Puts = putData
                .Select(x => new SmilePoint
                {
                    Strike = (double)x.ExercisePrice,
                    Iv = x.Iv,
                    Delta = x.Delta
                })
                .OrderBy(x => x.Strike)
                .ToList()
        };
    }

    /// <summary>期限结构：ATM IV vs 到期时间</summary>
    public async Task<TermStructureDto> GetTermStructureAsync(string underlying, DateTime date)
    {
        var tradeDate = date.Date;

        var ivData = await optRepo.GetIvGreeksAsync(tradeDate, underlying);
        var expiries = await optRepo.GetAvailableExpiriesAsync(underlying, tradeDate);
        var contracts = await optRepo.GetActiveContractsAsync(underlying, tradeDate);

        var underlyingDaily = await optRepo.GetLatestUnderlyingDailyAsync(underlying);
        double S = underlyingDaily != null ? (double)underlyingDaily.Close : 0;

        var points = new List<TermStructurePoint>();
        foreach (var expiry in expiries)
        {
            var expiryContracts = contracts.Where(c => c.MaturityDate.Date == expiry.Date).ToList();
            var expiryIv = (from c in expiryContracts
                            join iv in ivData on c.TsCode equals iv.TsCode
                            where iv.Iv.HasValue && iv.IvConfidence
                            select new
                            {
                                c.ExercisePrice,
                                Iv = (double)iv.Iv!.Value
                            }).ToList();

            if (expiryIv.Count == 0) continue;

            var atm = expiryIv
                .OrderBy(x => Math.Abs(S / (double)x.ExercisePrice - 1.0))
                .First();

            points.Add(new TermStructurePoint
            {
                Expiry = expiry,
                DaysToExpiry = (int)(expiry.Date - tradeDate).TotalDays,
                AtmIv = atm.Iv
            });
        }

        return new TermStructureDto
        {
            Underlying = underlying,
            Date = tradeDate.ToString("yyyy-MM-dd"),
            Points = points.OrderBy(p => p.Expiry).ToList()
        };
    }
}