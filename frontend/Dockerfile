# ── build stage ──
FROM node:20-alpine AS build
WORKDIR /app

# 依赖层缓存
COPY package.json package-lock.json ./
RUN npm ci

# 构建
COPY . .
# NEXT_PUBLIC_API_BASE_URL 需在构建时注入（客户端变量）
ARG NEXT_PUBLIC_API_BASE_URL=/api
ENV NEXT_PUBLIC_API_BASE_URL=${NEXT_PUBLIC_API_BASE_URL}
RUN npm run build

# ── runtime stage ──
FROM node:20-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production

COPY --from=build /app/package.json /app/package-lock.json ./
RUN npm ci --omit=dev

COPY --from=build /app/.next ./.next
COPY --from=build /app/public ./public
COPY --from=build /app/next.config.js ./next.config.js

EXPOSE 3000
CMD ["npm", "run", "start"]
