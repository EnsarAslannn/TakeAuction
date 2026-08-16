import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { VISUALS } from "@/content/catalog";
import { useAuthStore } from "@/store/authStore";
import { SplitLine } from "@/motion/Reveal";

export function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  const login = useAuthStore((state) => state.login);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const redirectTo = (location.state as { from?: string } | null)?.from ?? "/auctions";

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    setPending(true);
    setError(null);

    try {
      await login(email, password);
      navigate(redirectTo, { replace: true });
    } catch (caught) {
      setError((caught as Error).message);
    } finally {
      setPending(false);
    }
  };

  return (
    <div className="grid min-h-screen lg:grid-cols-2">
      <div className="relative hidden lg:block">
        {/* Sits behind the plate so the panel still reads as a room rather than
            a blank column if the image is missing. */}
        <div aria-hidden className="absolute inset-0 bg-gradient-to-br from-stone-dark via-ink-soft to-ink" />
        <img
          src={VISUALS.login}
          alt=""
          aria-hidden
          className="absolute inset-0 h-full w-full object-cover"
        />
        <div className="absolute inset-0 bg-ink/25" />
      </div>

      <div className="flex items-center justify-center bg-paper px-6 py-32">
        <div className="w-full max-w-md">
          <p className="eyebrow">Giriş</p>
          <h1 className="mt-6 font-display text-huge font-light leading-[0.95] text-ink">
            <SplitLine text="tekrar hoş geldiniz" />
          </h1>

          <form onSubmit={submit} className="mt-12 space-y-8">
            <div>
              <label htmlFor="email" className="eyebrow mb-3 block">
                E-posta
              </label>
              <input
                id="email"
                type="email"
                required
                autoComplete="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                className="field"
                placeholder="ornek@takeauction.local"
              />
            </div>

            <div>
              <label htmlFor="password" className="eyebrow mb-3 block">
                Parola
              </label>
              <input
                id="password"
                type="password"
                required
                autoComplete="current-password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                className="field"
                placeholder="••••••••••"
              />
            </div>

            {error && (
              <p className="border-l-2 border-sand-deep pl-4 font-sans text-sm leading-relaxed text-ink/70">
                {error}
              </p>
            )}

            <button type="submit" disabled={pending} className="btn-primary w-full">
              {pending ? "Giriş yapılıyor…" : "Giriş yapın"}
            </button>
          </form>

          <p className="mt-10 border-t border-ink/12 pt-8 font-sans text-sm text-ink/55">
            Hesabınız yok mu?{" "}
            <Link to="/register" className="text-sand-deep underline underline-offset-4">
              Kaydolun
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
