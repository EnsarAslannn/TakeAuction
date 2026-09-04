import { useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import {
  ACCEPTED_IMAGE_TYPES,
  MAX_IMAGE_SIZE_BYTES,
  createAuction,
  uploadAuctionImage,
} from "@/api/auctions";
import { ApiError, toApiError } from "@/api/client";
import { ImageDropzone } from "@/components/ImageDropzone";
import { VISUALS } from "@/content/catalog";
import { useFormat, useT, type Translate } from "@/i18n";
import { SplitLine } from "@/motion/Reveal";

function toLocalInputValue(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
}

function formatDuration(t: Translate, startsAt: string, endsAt: string): string | null {
  const span = new Date(endsAt).getTime() - new Date(startsAt).getTime();
  if (!Number.isFinite(span) || span <= 0) return null;

  const days = Math.floor(span / 86_400_000);
  const hours = Math.floor((span % 86_400_000) / 3_600_000);
  const minutes = Math.floor((span % 3_600_000) / 60_000);

  const parts = [
    days > 0 ? t(days === 1 ? "create.duration.day" : "create.duration.days", { n: days }) : null,
    hours > 0 ? t(hours === 1 ? "create.duration.hour" : "create.duration.hours", { n: hours }) : null,
    days === 0 && minutes > 0
      ? t(minutes === 1 ? "create.duration.minute" : "create.duration.minutes", { n: minutes })
      : null,
  ].filter(Boolean);

  return parts.length > 0 ? parts.join(" ") : t("create.duration.short");
}

const DEFAULT_START = new Date(Date.now() + 2 * 60_000);
const DEFAULT_END = new Date(Date.now() + 26 * 60 * 60_000);

export function CreateAuction() {
  const navigate = useNavigate();

  const [form, setForm] = useState({
    title: "",
    description: "",
    startingPrice: "1000",
    minimumBidIncrement: "50",
    startsAt: toLocalInputValue(DEFAULT_START),
    endsAt: toLocalInputValue(DEFAULT_END),
  });
  const [imageUrl, setImageUrl] = useState<string | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [imageError, setImageError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [pending, setPending] = useState(false);
  const [visualFailed, setVisualFailed] = useState(false);
  const t = useT();
  const format = useFormat();

  const objectUrl = useRef<string | null>(null);

  useEffect(
    () => () => {
      if (objectUrl.current) URL.revokeObjectURL(objectUrl.current);
    },
    []
  );

  const errorFor = (field: string) =>
    fieldErrors[field]?.[0] ??
    fieldErrors[field.charAt(0).toUpperCase() + field.slice(1)]?.[0] ??
    null;

  const releasePreview = () => {
    if (objectUrl.current) {
      URL.revokeObjectURL(objectUrl.current);
      objectUrl.current = null;
    }
  };

  const clearImage = () => {
    releasePreview();
    setPreview(null);
    setImageUrl(null);
    setImageError(null);
  };

  const selectImage = async (file: File) => {
    if (!ACCEPTED_IMAGE_TYPES.includes(file.type)) {
      setImageError(t("create.image.typeError"));
      return;
    }

    if (file.size > MAX_IMAGE_SIZE_BYTES) {
      setImageError(
        t("create.image.sizeError", { mb: Math.round(MAX_IMAGE_SIZE_BYTES / (1024 * 1024)) })
      );
      return;
    }

    releasePreview();
    objectUrl.current = URL.createObjectURL(file);

    setPreview(objectUrl.current);
    setImageError(null);
    setImageUrl(null);
    setUploading(true);

    try {
      const uploaded = await uploadAuctionImage(file);
      setImageUrl(uploaded.url);
    } catch (caught) {
      const apiError = toApiError(caught);
      setImageError(apiError.message);
      releasePreview();
      setPreview(null);
    } finally {
      setUploading(false);
    }
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setPending(true);
    setError(null);
    setFieldErrors({});

    try {
      const created = await createAuction({
        title: form.title,
        description: form.description,
        startingPrice: Number(form.startingPrice),
        minimumBidIncrement: Number(form.minimumBidIncrement),
        startsAtUtc: new Date(form.startsAt).toISOString(),
        endsAtUtc: new Date(form.endsAt).toISOString(),
        imageUrl,
      });
      navigate(`/auctions/${created.id}`);
    } catch (caught) {
      if (caught instanceof ApiError) {
        setFieldErrors(caught.fieldErrors);
        setError(caught.message);
      } else {
        setError((caught as Error).message);
      }
    } finally {
      setPending(false);
    }
  };

  const duration = formatDuration(t, form.startsAt, form.endsAt);
  const startingPrice = Number(form.startingPrice);

  return (
    <div className="min-h-screen bg-paper pb-32">
      <section
        data-nav-theme="dark"
        className="relative flex min-h-[62vh] items-end overflow-hidden bg-ink pb-16 pt-40 md:min-h-[68vh] md:pb-20"
      >
        <div aria-hidden className="absolute inset-0 bg-gradient-to-br from-stone-dark via-ink-soft to-ink" />
        {!visualFailed && (
          <img
            src={VISUALS.create}
            alt=""
            aria-hidden
            onError={() => setVisualFailed(true)}
            className="absolute inset-0 h-full w-full object-cover"
          />
        )}
        <div
          aria-hidden
          className="absolute inset-0"
          style={{
            background:
              "linear-gradient(180deg, rgba(26,24,21,0.9) 0%, rgba(26,24,21,0.5) 26%, rgba(26,24,21,0.55) 55%, rgba(26,24,21,0.97) 100%)",
          }}
        />
        <div aria-hidden className="absolute inset-0 bg-ink/25" />

        <div className="shell relative mx-auto w-full max-w-shell">
          <div className="max-w-3xl">
            <p className="font-mono text-eyebrow uppercase text-sand">{t("create.eyebrow")}</p>
            <h1 className="mt-6 font-display text-giant font-light leading-[0.9] text-paper">
              <SplitLine text={t("create.title")} />
            </h1>
            <div aria-hidden className="mt-10 h-px w-24 bg-sand/70" />
            <p className="mt-8 max-w-[54ch] font-sans text-base leading-relaxed text-paper/70">
              {t("create.lede")}
            </p>
          </div>
        </div>
      </section>

      <div className="shell mx-auto mt-20 max-w-shell md:mt-28">
        <form onSubmit={submit} className="grid gap-16 lg:grid-cols-12 lg:gap-12">
          <div className="lg:col-span-7 lg:col-start-6">
            <div className="space-y-16">
              <Section index="01" title={t("create.section.lot")}>
                <div>
                  <label htmlFor="title" className="eyebrow mb-3 block">
                    {t("create.titleLabel")}
                  </label>
                  <input
                    id="title"
                    required
                    minLength={3}
                    maxLength={200}
                    value={form.title}
                    onChange={(event) => setForm({ ...form, title: event.target.value })}
                    className="field font-display text-2xl"
                    placeholder={t("create.titlePlaceholder")}
                  />
                  {errorFor("title") && (
                    <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("title")}</p>
                  )}
                </div>

                <div>
                  <label htmlFor="description" className="eyebrow mb-3 block">
                    {t("create.descriptionLabel")}
                  </label>
                  <textarea
                    id="description"
                    required
                    minLength={10}
                    maxLength={4000}
                    rows={5}
                    value={form.description}
                    onChange={(event) => setForm({ ...form, description: event.target.value })}
                    className="field resize-none"
                    placeholder={t("create.descriptionPlaceholder")}
                  />
                  <p className="mt-2 font-mono text-eyebrow uppercase tabular-nums text-stone">
                    {form.description.length} / 4000
                  </p>
                  {errorFor("description") && (
                    <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("description")}</p>
                  )}
                </div>
              </Section>

              <Section
                index="02"
                title={t("create.section.image")}
                note={t("create.section.imageNote")}
              >
                <ImageDropzone
                  preview={preview}
                  uploading={uploading}
                  error={imageError}
                  onSelect={selectImage}
                  onClear={clearImage}
                />
                {errorFor("imageUrl") && (
                  <p className="font-sans text-xs text-sand-deep">{errorFor("imageUrl")}</p>
                )}
              </Section>

              <Section index="03" title={t("create.section.pricing")}>
                <div className="grid gap-8 sm:grid-cols-2">
                  <div>
                    <label htmlFor="startingPrice" className="eyebrow mb-3 block">
                      {t("create.startingPrice")}
                    </label>
                    <input
                      id="startingPrice"
                      type="number"
                      step="0.01"
                      min="0.01"
                      required
                      value={form.startingPrice}
                      onChange={(event) => setForm({ ...form, startingPrice: event.target.value })}
                      className="field tabular-nums"
                    />
                    {errorFor("startingPrice") && (
                      <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("startingPrice")}</p>
                    )}
                  </div>

                  <div>
                    <label htmlFor="minimumBidIncrement" className="eyebrow mb-3 block">
                      {t("create.minIncrement")}
                    </label>
                    <input
                      id="minimumBidIncrement"
                      type="number"
                      step="0.01"
                      min="0.01"
                      required
                      value={form.minimumBidIncrement}
                      onChange={(event) =>
                        setForm({ ...form, minimumBidIncrement: event.target.value })
                      }
                      className="field tabular-nums"
                    />
                    {errorFor("minimumBidIncrement") && (
                      <p className="mt-2 font-sans text-xs text-sand-deep">
                        {errorFor("minimumBidIncrement")}
                      </p>
                    )}
                  </div>
                </div>
              </Section>

              <Section index="04" title={t("create.section.schedule")}>
                <div className="grid gap-8 sm:grid-cols-2">
                  <div>
                    <label htmlFor="startsAt" className="eyebrow mb-3 block">
                      {t("create.startsAt")}
                    </label>
                    <input
                      id="startsAt"
                      type="datetime-local"
                      required
                      value={form.startsAt}
                      onChange={(event) => setForm({ ...form, startsAt: event.target.value })}
                      className="field"
                    />
                    {errorFor("startsAtUtc") && (
                      <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("startsAtUtc")}</p>
                    )}
                  </div>

                  <div>
                    <label htmlFor="endsAt" className="eyebrow mb-3 block">
                      {t("create.endsAt")}
                    </label>
                    <input
                      id="endsAt"
                      type="datetime-local"
                      required
                      value={form.endsAt}
                      onChange={(event) => setForm({ ...form, endsAt: event.target.value })}
                      className="field"
                    />
                    {errorFor("endsAtUtc") && (
                      <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("endsAtUtc")}</p>
                    )}
                  </div>
                </div>
              </Section>

              <div className="border-t border-ink/12 pt-10">
                {error && (
                  <p className="mb-8 border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70">
                    {error}
                  </p>
                )}

                <div className="flex flex-wrap items-center gap-x-8 gap-y-4">
                  <button type="submit" disabled={pending || uploading} className="btn-primary">
                    {pending ? t("create.publishing") : t("create.publish")}
                  </button>
                  <p className="max-w-[42ch] font-sans text-xs leading-relaxed text-ink/45">
                    {t("create.publishNote")}
                  </p>
                </div>
              </div>
            </div>
          </div>

          <aside className="lg:col-span-4 lg:col-start-1 lg:row-start-1">
            <div className="lg:sticky lg:top-28">
              <p className="eyebrow">{t("create.preview")}</p>

              <div className="relative mt-6 aspect-[4/5] overflow-hidden bg-ink">
                {preview ? (
                  <>
                    <div
                      aria-hidden
                      className="absolute inset-0 scale-110 bg-cover bg-center opacity-25 blur-2xl"
                      style={{ backgroundImage: `url(${preview})` }}
                    />
                    <img
                      src={preview}
                      alt=""
                      aria-hidden
                      className="absolute inset-0 h-full w-full object-contain p-6"
                    />
                  </>
                ) : (
                  <div className="absolute inset-5 flex items-center justify-center border border-paper/12">
                    <span className="font-mono text-eyebrow uppercase text-paper/30">
                      {t("create.preview.noImage")}
                    </span>
                  </div>
                )}

                <div
                  aria-hidden
                  className="pointer-events-none absolute inset-0"
                  style={{
                    background:
                      "radial-gradient(65% 55% at 50% 40%, rgba(192,160,112,0.18) 0%, transparent 72%)",
                  }}
                />
              </div>

              <h2 className="mt-8 text-balance font-display text-3xl font-light leading-tight text-ink">
                {form.title.trim() || t("create.preview.untitled")}
              </h2>

              <dl className="mt-8 space-y-4 border-t border-ink/12 pt-6">
                <PreviewRow
                  label={t("create.preview.opening")}
                  value={
                    Number.isFinite(startingPrice) && startingPrice > 0
                      ? format.money(startingPrice)
                      : "—"
                  }
                />
                <PreviewRow
                  label={t("create.preview.duration")}
                  value={duration ?? t("create.preview.invalidRange")}
                />
                <PreviewRow
                  label={t("create.preview.image")}
                  value={
                    imageUrl ? t("create.preview.imageAdded") : t("create.preview.imageNone")
                  }
                />
              </dl>
            </div>
          </aside>
        </form>
      </div>
    </div>
  );
}

function Section({
  index,
  title,
  note,
  children,
}: {
  index: string;
  title: string;
  note?: string;
  children: React.ReactNode;
}) {
  return (
    <section>
      <div className="flex items-baseline gap-5 border-b border-ink/12 pb-4">
        <span className="font-mono text-eyebrow tabular-nums text-sand-deep">{index}</span>
        <h2 className="font-display text-xl font-light text-ink">{title}</h2>
        {note && <span className="ml-auto font-mono text-eyebrow uppercase text-stone">{note}</span>}
      </div>
      <div className="mt-8 space-y-8">{children}</div>
    </section>
  );
}

function PreviewRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="font-mono text-eyebrow uppercase text-stone">{label}</dt>
      <dd className="text-right font-sans text-sm tabular-nums text-ink">{value}</dd>
    </div>
  );
}
