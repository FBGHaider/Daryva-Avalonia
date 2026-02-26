/** @type {import('next').NextConfig} */
const nextConfig = {
  output: "export",
  images: {
    unoptimized: true,
    remotePatterns: [
      { protocol: "https", hostname: "**.daryva.com" },
      { protocol: "https", hostname: "daryva.com" },
    ],
  },
};

module.exports = nextConfig;
