import { ScreenshotPlaceholder } from "./screenshot-placeholder";

const screens = [
  "Dashboard",
  "Tenants",
  "Payments",
  "Expenses",
  "Documents",
];

export function ScreenshotGallery() {
  return (
    <section className="py-20 md:py-28 bg-background" aria-labelledby="screens-heading">
      <div className="mx-auto max-w-6xl px-6">
        <h2 id="screens-heading" className="font-heading text-3xl font-bold text-center text-primary mb-4">
          See Daryva in action
        </h2>
        <p className="text-center text-text-muted max-w-xl mx-auto mb-16">
          A quick tour of the key screens. Your data, your clarity.
        </p>
        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
          {screens.map((label) => (
            <div
              key={label}
              className="group transition-transform hover:scale-[1.02] focus-within:scale-[1.02]"
            >
              <ScreenshotPlaceholder label={label} />
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
