/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  // Plotly.js is large (~3MB); ensure it is transpiled correctly
  transpilePackages: ["react-plotly.js", "plotly.js-dist-min"],
  experimental: {
    // Allow large packages to be optimized
    optimizePackageImports: ["lucide-react", "date-fns"],
  },
  // Rewrites: route /api/* to backend so we can use same-origin fetch
  async rewrites() {
    const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5000";
    return [
      {
        source: "/api/:path*",
        destination: `${apiBase}/api/:path*`,
      },
    ];
  },
};

module.exports = nextConfig;
