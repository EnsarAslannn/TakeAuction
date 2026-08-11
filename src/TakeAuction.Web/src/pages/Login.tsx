import { useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { VISUALS } from "@/content/catalog";
import { useAuthStore } from "@/store/authStore";
import { SplitLine } from "@/motion/Reveal";

const DEMO_ACCOUNTS = [
  { label: "Satıcı", email: "seller@takeauction.local" },
  { label: "Alıcı", email: "bidder@takeauction.local" },
];

const DEMO_PASSWORD = "TakeAuction!2026";

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
        <img src={VISUALS.vault} alt="" aria-hidden className="h-full w-full object-cover" />
        <div className="absolute inset-0 bg-ink/25" />
        <div className="absolute bottom-14 left-14 right-14">
          <p className="font-display text-4xl font-light leading-tight text-paper">
            Oturumun HttpOnly çerezde. JavaScript token'a dokunamaz.
          </p>
        </div>
      </div>

      <div className="flex items-center justify-center bg-paper px-6 py-32">
        <div className="w-full max-w-md">
          <p className="eyebrow">Giriş</p>
          <h1 className="mt-6 font-display text-huge font-light leading-[0.95] text-ink">
            <SplitLine text="tekrar hoş geldin" />
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
              {pending ? "Giriş yapılıyor…" : "Giriş yap"}
            </button>
          </form>

          <div className="mt-10 border-t border-ink/12 pt-8">
            <p className="eyebrow mb-4">Demo hesapları</p>
            <div className="flex flex-wrap gap-2">
              {DEMO_ACCOUNTS.map((account) => (
                <button
                  key={account.email}
                  type="button"
                  onClick={() => {
                    setEmail(account.email);
                    setPassword(DEMO_PASSWORD);
                  }}
                  className="rounded-full border border-ink/15 px-4 py-1.5 font-mono text-eyebrow uppercase text-stone transition-colors hover:border-ink/40 hover:text-ink"
                >
                  {account.label}
                </button>
              ))}
            </div>
          </div>

          <p className="mt-8 font-sans text-sm text-ink/55">
            Hesabın yok mu?{" "}
            <Link to="/register" className="text-sand-deep underline underline-offset-4">
              Kaydol
            </Link>
          </p>
        </div>
      </div>
    </div>
  );
}
