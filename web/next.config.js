/** @type {import('next').NextConfig} */
const nextConfig = {
  images: {
    remotePatterns: [
      { protocol: 'https', hostname: '**.daryva.com' },
      { protocol: 'https', hostname: 'daryva.com' },
    ],
  },
};

module.exports = nextConfig;
