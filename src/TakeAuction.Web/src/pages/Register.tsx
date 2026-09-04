import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError } from "@/api/client";
import { VISUALS } from "@/content/catalog";
import { useAuthStore } from "@/store/authStore";
import { SplitLine } from "@/motion/Reveal";
import { useT, type TranslationKey } from "@/i18n";
import type { UserRole } from "@/api/types";

const ROLES: {
  value: Exclude<UserRole, "Admin">;
  labelKey: TranslationKey;
  hintKey: TranslationKey;
}[] = [
  { value: "Bidder", labelKey: "register.role.bidder", hintKey: "register.role.bidderHint" },
  { value: "Seller", labelKey: "register.role.seller", hintKey: "register.role.sellerHint" },
];

export function Register() {
  const navigate = useNavigate();
  const register = useAuthStore((state) => state.register);
  const t = useT();

  const [form, setForm] = useState({
    email: "",
    displayName: "",
    password: "",
    role: "Bidder" as Exclude<UserRole, "Admin">,
  });
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});
  const [pending, setPending] = useState(false);

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setPending(true);
    setError(null);
    setFieldErrors({});

    try {
      await register(form);
      navigate("/auctions", { replace: true });
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

  const errorFor = (field: string) =>
    fieldErrors[field]?.[0] ??
    fieldErrors[field.charAt(0).toUpperCase() + field.slice(1)]?.[0] ??
    null;

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="flex items-center justify-center bg-paper px-6 py-32">
        <div className="w-full max-w-md">
          <p className="eyebrow">{t("register.eyebrow")}</p>
          <h1 className="mt-6 font-display text-huge font-light leading-[0.95] text-ink">
            <SplitLine text={t("register.title")} />
          </h1>

          <form onSubmit={submit} className="mt-12 space-y-8">
            <div>
              <label htmlFor="displayName" className="eyebrow mb-3 block">
                {t("register.displayName")}
              </label>
              <input
                id="displayName"
                required
                value={form.displayName}
                onChange={(event) => setForm({ ...form, displayName: event.target.value })}
                className="field"
                placeholder={t("register.displayNamePlaceholder")}
              />
              {errorFor("displayName") && (
                <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("displayName")}</p>
              )}
            </div>

            <div>
              <label htmlFor="reg-email" className="eyebrow mb-3 block">
                {t("register.email")}
              </label>
              <input
                id="reg-email"
                type="email"
                required
                autoComplete="email"
                value={form.email}
                onChange={(event) => setForm({ ...form, email: event.target.value })}
                className="field"
                placeholder={t("register.emailPlaceholder")}
              />
              {errorFor("email") && (
                <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("email")}</p>
              )}
            </div>

            <div>
              <label htmlFor="reg-password" className="eyebrow mb-3 block">
                {t("register.password")}
              </label>
              <input
                id="reg-password"
                type="password"
                required
                autoComplete="new-password"
                value={form.password}
                onChange={(event) => setForm({ ...form, password: event.target.value })}
                className="field"
                placeholder={t("register.passwordPlaceholder")}
              />
              <p className="mt-2 font-sans text-xs text-stone">{t("register.passwordHint")}</p>
              {errorFor("password") && (
                <p className="mt-2 font-sans text-xs text-sand-deep">{errorFor("password")}</p>
              )}
            </div>

            <div>
              <span className="eyebrow mb-4 block">{t("register.role")}</span>
              <div className="grid gap-3 sm:grid-cols-2">
                {ROLES.map((role) => (
                  <button
                    key={role.value}
                    type="button"
                    onClick={() => setForm({ ...form, role: role.value })}
                    className={`border p-5 text-left transition-all duration-500 ease-editorial ${
                      form.role === role.value
                        ? "border-ink bg-ink text-paper"
                        : "border-ink/15 text-ink hover:border-ink/40"
                    }`}
                  >
                    <span className="block font-display text-lg font-light">
                      {t(role.labelKey)}
                    </span>
                    <span
                      className={`mt-1.5 block font-sans text-xs leading-relaxed ${
                        form.role === role.value ? "text-paper/60" : "text-stone"
                      }`}
                    >
                      {t(role.hintKey)}
                    </span>
                  </button>
                ))}
              </div>
            </div>

            {error && (
              <p className="border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70">
                {error}
              </p>
            )}

            <button type="submit" disabled={pending} className="btn-primary w-full">
              {pending ? t("register.submitting") : t("register.submit")}
            </button>
          </form>

          <p className="mt-8 font-sans text-sm text-ink/55">
            {t("register.haveAccount")}{" "}
            <Link to="/login" className="text-sand-deep underline underline-offset-4">
              {t("register.login")}
            </Link>
          </p>
        </div>
      </div>

      <div className="relative hidden lg:block">
        <div aria-hidden className="absolute inset-0 bg-gradient-to-br from-stone-dark via-ink-soft to-ink" />
        <img
          src={VISUALS.gallery}
          alt=""
          aria-hidden
          className="absolute inset-0 h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-ink/20" />
      </div>
    </div>
  );
}
