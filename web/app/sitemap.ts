import { MetadataRoute } from "next";
import { site } from "@/lib/site";

const routes = ["", "/product", "/pricing", "/demo", "/security", "/about", "/contact", "/legal/privacy", "/legal/terms"];

export default function sitemap(): MetadataRoute.Sitemap {
  return routes.map((path) => ({
    url: path ? `${site.url}${path}` : site.url,
    lastModified: new Date(),
    changeFrequency: path === "" ? "weekly" : "monthly",
    priority: path === "" ? 1 : 0.8,
  }));
}
