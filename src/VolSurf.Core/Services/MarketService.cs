using VolSurf.Core.Models.Dto;
using VolSurf.Data.Repositories;

namespace VolSurf.Core.Services;

/// <summary>
/// 市场表现计算服务。
///
/// 从 StockDaily 表计算：
/// - 最新价
/// - 涨跌幅（1月/3月/1年/YTD）
/// - 相对沪深300超额收益
/// - 均线（MA20/MA60/MA120/MA250）
/// - 年化波动率（近60日）
/// - 均线趋势判断（多头排列/空头排列/混合）
/// </summary>
public class MarketService(IStockRepository repo)
{
    /// <summary>获取市场表现完整报告</summary>
    public async Task<MarketMetricsDto> GetMarketMetricsAsync(string tsCode)
    {
        // 1. 获取近1年日线数据（约250个交易日）
        var daily = await repo.GetRecentStockDailyAsync(tsCode, 250);
        if (daily.Count == 0)
            return new MarketMetricsDto();

        var latest = daily[^1];
        double latestPrice = latest.Close.HasValue ? (double)latest.Close.Value : 0;

        // 2. 涨跌幅计算
        double pctChg1M = CalcPeriodReturn(daily, 20);
        double pctChg3M = CalcPeriodReturn(daily, 60);
        double pctChg1Y = CalcPeriodReturn(daily, 250);
        double pctChgYTD = CalcYtdReturn(daily);

        // 3. vs 沪深300超额收益
        var hs300 = await repo.GetHs300DailyAsync(250);
        double hs300Return1Y = CalcPeriodReturn(hs300, 250);
        double vsHs3001Y = pctChg1Y - hs300Return1Y;

        // 4. 均线计算
        double ma20 = CalcMA(daily, 20);
        double ma60 = CalcMA(daily, 60);
        double ma120 = CalcMA(daily, 120);
        double ma250 = CalcMA(daily, 250);

        // 5. 均线趋势判断
        // 多头排列：ma20 > ma60 > ma120 > ma250
        // 空头排列：ma20 < ma60 < ma120 < ma250
        // 其他：mixed
        string maTrend;
        if (ma20 > ma60 && ma60 > ma120 && ma120 > ma250)
            maTrend = "bull";
        else if (ma20 < ma60 && ma60 < ma120 && ma120 < ma250)
            maTrend = "bear";
        else
            maTrend = "mixed";

        // 6. 近60日年化波动率
        double volatility = CalcAnnualizedVolatility(daily, 60);

        // 7. 价格序列（前端K线用独立API加载，这里不返回全部）
        var priceTrend = daily.TakeLast(250).Select(d => new PricePointDto
        {
            Date = d.TradeDate.ToString("yyyy-MM-dd"),
            Open = d.Open.HasValue ? (double)d.Open : 0,
            High = d.High.HasValue ? (double)d.High : 0,
            Low = d.Low.HasValue ? (double)d.Low : 0,
            Close = d.Close.HasValue ? (double)d.Close : 0,
            Volume = d.Vol.HasValue ? (double)d.Vol : 0,
            Ma20 = null,  // 前端从均线数据自行叠加
            Ma60 = null,
            Ma120 = null
        }).ToList();

        return new MarketMetricsDto
        {
            Price = Math.Round(latestPrice, 2),
            PctChg1M = Math.Round(pctChg1M, 2),
            PctChg3M = Math.Round(pctChg3M, 2),
            PctChg1Y = Math.Round(pctChg1Y, 2),
            PctChgYTD = Math.Round(pctChgYTD, 2),
            VsHs3001Y = Math.Round(vsHs3001Y, 2),
            Ma20 = Math.Round(ma20, 2),
            Ma60 = Math.Round(ma60, 2),
            Ma120 = Math.Round(ma120, 2),
            Ma250 = Math.Round(ma250, 2),
            MaTrend = maTrend,
            Volatility = Math.Round(volatility, 2),
            PriceTrend = priceTrend
        };
    }

    /// <summary>计算近N日涨跌幅</summary>
    private double CalcPeriodReturn(List<Data.Entities.StockDaily> daily, int days)
    {
        if (daily.Count < days + 1) return 0;

        var latest = daily[^1];
        var start = daily[^Math.Min(days + 1, daily.Count)];

        if (start.Close == null || start.Close == 0 || latest.Close == null)
            return 0;

        return ((double)latest.Close - (double)start.Close) / (double)start.Close * 100;
    }

    /// <summary>计算年初至今涨跌幅</summary>
    private double CalcYtdReturn(List<Data.Entities.StockDaily> daily)
    {
        if (daily.Count == 0) return 0;

        var latest = daily[^1];
        int year = latest.TradeDate.Year;

        // 找到当年第一个交易日
        var ytdStart = daily.FirstOrDefault(d => d.TradeDate.Year == year);
        if (ytdStart == null || ytdStart.Close == null || ytdStart.Close == 0 || latest.Close == null)
            return 0;

        return ((double)latest.Close - (double)ytdStart.Close) / (double)ytdStart.Close * 100;
    }

    /// <summary>计算简单移动平均线（MA）</summary>
    private double CalcMA(List<Data.Entities.StockDaily> daily, int period)
    {
        if (daily.Count < period) period = daily.Count;

        var recent = daily.TakeLast(period)
            .Where(d => d.Close.HasValue)
            .Select(d => (double)d.Close!.Value)
            .ToList();

        if (recent.Count == 0) return 0;
        return recent.Average();
    }

    /// <summary>计算近N日年化波动率 = std(daily_returns) * sqrt(250)</summary>
    private double CalcAnnualizedVolatility(List<Data.Entities.StockDaily> daily, int days)
    {
        if (daily.Count < days + 1) return 0;

        // 取近N日收盘价
        var prices = daily.TakeLast(days + 1)
            .Where(d => d.Close.HasValue)
            .Select(d => (double)d.Close!.Value)
            .ToList();

        if (prices.Count < 2) return 0;

        // 计算日收益率序列
        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1] != 0)
                returns.Add((prices[i] - prices[i - 1]) / prices[i - 1]);
        }

        if (returns.Count == 0) return 0;

        // 标准差 × sqrt(250) = 年化波动率
        double mean = returns.Average();
        double variance = returns.Average(r => Math.Pow(r - mean, 2));
        double std = Math.Sqrt(variance);

        return std * Math.Sqrt(250) * 100; // 转为百分比
    }
}
