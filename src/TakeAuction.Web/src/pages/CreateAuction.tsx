import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { createAuction } from "@/api/auctions";
import { ApiError } from "@/api/client";
import { SplitLine } from "@/motion/Reveal";

function toLocalInputValue(date: Date): string {
  const offset = date.getTimezoneOffset() * 60_000;
  return new Date(date.getTime() - offset).toISOString().slice(0, 16);
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
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [pending, setPending] = useState(false);

  const errorFor = (field: string) =>
    fieldErrors[field]?.[0] ??
    fieldErrors[field.charAt(0).toUpperCase() + field.slice(1)]?.[0] ??
    null;

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

  return (
    <div className="min-h-screen bg-paper pb-32 pt-36 md:pt-44">
      <div className="shell mx-auto max-w-3xl">
        <p className="eyebrow">Satıcı</p>
        <h1 className="mt-6 font-display text-giant font-light leading-[0.9] text-ink">
          <SplitLine text="yeni ilan" />
        </h1>

        <p className="mt-8 max-w-[52ch] font-sans text-base leading-relaxed text-ink/65">
          Parçanızı ne kadar iyi anlatırsanız o kadar iyi teklif alırsınız. Açık artırma en az 5 dakika,
          en fazla 30 gün sürebilir; başlangıç zamanı geçmişte olamaz. Yayınladıktan sonra kapanışla
          ilgilenmeniz gerekmez — süre dolduğunda salon kendiliğinden kapanır.
        </p>

        <form onSubmit={submit} className="mt-14 space-y-10">
          <div>
            <label htmlFor="title" className="eyebrow mb-3 block">
              Başlık
            </label>
            <input
              id="title"
              required
              minLength={3}
              maxLength={200}
              value={form.title}
              onChange={(event) => setForm({ ...form, title: event.target.value })}
              className="field font-display text-2xl"
              placeholder="Örn. 1968 Omega Seamaster"
            />
            {errorFor("title") && (
              <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("title")}</p>
            )}
          </div>

          <div>
            <label htmlFor="description" className="eyebrow mb-3 block">
              Açıklama
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
              placeholder="Parçanın durumu, kökeni ve varsa belgeleri…"
            />
            {errorFor("description") && (
              <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("description")}</p>
            )}
          </div>

          <div className="grid gap-8 sm:grid-cols-2">
            <div>
              <label htmlFor="startingPrice" className="eyebrow mb-3 block">
                Başlangıç fiyatı (₺)
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
                Minimum artış (₺)
              </label>
              <input
                id="minimumBidIncrement"
                type="number"
                step="0.01"
                min="0.01"
                required
                value={form.minimumBidIncrement}
                onChange={(event) => setForm({ ...form, minimumBidIncrement: event.target.value })}
                className="field tabular-nums"
              />
              {errorFor("minimumBidIncrement") && (
                <p className="mt-2 font-sans text-xs text-sand-deep">
                  {errorFor("minimumBidIncrement")}
                </p>
              )}
            </div>

            <div>
              <label htmlFor="startsAt" className="eyebrow mb-3 block">
                Başlama zamanı
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
                Bitiş zamanı
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

          {error && (
            <p className="border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70">
              {error}
            </p>
          )}

          <button type="submit" disabled={pending} className="btn-primary">
            {pending ? "Yayınlanıyor…" : "İlanı yayınlayın"}
          </button>
        </form>
      </div>
    </div>
  );
}
