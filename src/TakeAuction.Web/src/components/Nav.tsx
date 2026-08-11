import { useEffect, useState } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import { canSell, useAuthStore } from "@/store/authStore";
import { useNavTheme } from "@/lib/useNavTheme";

const LINKS = [
  { to: "/auctions", label: "Açık artırmalar" },
  { to: "/#how-it-works", label: "Nasıl çalışır" },
  { to: "/#capabilities", label: "Neler yapabilirsin" },
];

const ctaClass = (dark: boolean) =>
  `btn !px-5 !py-2.5 ${dark ? "bg-paper text-ink hover:bg-sand" : "bg-ink text-paper hover:bg-sand-deep"}`;

export function Nav() {
  const [scrolled, setScrolled] = useState(false);
  const [open, setOpen] = useState(false);
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);
  const navigate = useNavigate();
  const location = useLocation();
  const theme = useNavTheme();
  const dark = theme === "dark";

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 24);
    onScroll();
    window.addEventListener("scroll", onScroll, { passive: true });
    return () => window.removeEventListener("scroll", onScroll);
  }, []);

  useEffect(() => setOpen(false), [location.pathname]);

  const handleLogout = async () => {
    await logout();
    navigate("/");
  };

  return (
    <header
      className={`fixed inset-x-0 top-0 z-50 transition-all duration-700 ease-editorial ${
        scrolled ? (dark ? "bg-ink/70 backdrop-blur-xl" : "bg-paper/85 backdrop-blur-xl") : "bg-transparent"
      }`}
    >
      <div className={`shell mx-auto max-w-shell ${scrolled ? "py-4" : "py-6"} transition-all duration-700`}>
        <div className="flex items-center justify-between gap-6">
          <Link
            to="/"
            className={`font-display text-lg font-medium tracking-tightest transition-colors duration-700 hover:opacity-60 ${
              dark ? "text-paper" : "text-ink"
            }`}
          >
            take<span className={dark ? "text-sand" : "text-sand-deep"}>auction</span>
          </Link>

          <nav className="hidden items-center gap-9 md:flex">
            {LINKS.map((link) => (
              <NavLink
                key={link.to}
                to={link.to}
                className={({ isActive }) =>
                  `font-mono text-eyebrow uppercase transition-colors duration-500 ${
                    dark
                      ? isActive && link.to === "/auctions"
                        ? "text-paper"
                        : "text-paper/50 hover:text-paper"
                      : isActive && link.to === "/auctions"
                        ? "text-ink"
                        : "text-stone hover:text-ink"
                  }`
                }
              >
                {link.label}
              </NavLink>
            ))}
          </nav>

          <div className="hidden items-center gap-3 md:flex">
            {user ? (
              <>
                {canSell(user) && (
                  <Link
                    to="/auctions/new"
                    className={`btn !px-5 !py-2.5 border ${
                      dark
                        ? "border-paper/25 text-paper hover:border-sand hover:bg-sand hover:text-ink"
                        : "border-ink/20 text-ink hover:border-ink hover:bg-ink hover:text-paper"
                    }`}
                  >
                    İlan aç
                  </Link>
                )}
                <span
                  className={`font-mono text-eyebrow uppercase transition-colors duration-500 ${
                    dark ? "text-paper/50" : "text-stone"
                  }`}
                >
                  {user.displayName}
                </span>
                <button type="button" onClick={handleLogout} className={ctaClass(dark)}>
                  Çıkış
                </button>
              </>
            ) : (
              <>
                <Link
                  to="/login"
                  className={`font-mono text-eyebrow uppercase transition-colors duration-500 ${
                    dark ? "text-paper/50 hover:text-paper" : "text-stone hover:text-ink"
                  }`}
                >
                  Giriş
                </Link>
                <Link to="/register" className={ctaClass(dark)}>
                  Kaydol
                </Link>
              </>
            )}
          </div>

          <button
            type="button"
            onClick={() => setOpen((value) => !value)}
            aria-label="Menü"
            aria-expanded={open}
            className="flex h-9 w-9 flex-col items-center justify-center gap-1.5 md:hidden"
          >
            <span
              className={`h-px w-5 transition-all duration-300 ${dark ? "bg-paper" : "bg-ink"} ${
                open ? "translate-y-[3.5px] rotate-45" : ""
              }`}
            />
            <span
              className={`h-px w-5 transition-all duration-300 ${dark ? "bg-paper" : "bg-ink"} ${
                open ? "-translate-y-[3.5px] -rotate-45" : ""
              }`}
            />
          </button>
        </div>
      </div>

      <div
        className={`overflow-hidden bg-paper/95 backdrop-blur-xl transition-[max-height] duration-700 ease-editorial md:hidden ${
          open ? "max-h-96" : "max-h-0"
        }`}
      >
        <nav className="shell mx-auto flex max-w-shell flex-col gap-5 py-8">
          {LINKS.map((link) => (
            <Link key={link.to} to={link.to} className="font-display text-2xl font-light text-ink">
              {link.label}
            </Link>
          ))}
          <div className="rule my-2" />
          {user ? (
            <>
              {canSell(user) && (
                <Link to="/auctions/new" className="font-display text-2xl font-light text-ink">
                  İlan aç
                </Link>
              )}
              <button
                type="button"
                onClick={handleLogout}
                className="text-left font-display text-2xl font-light text-sand-deep"
              >
                Çıkış yap
              </button>
            </>
          ) : (
            <>
              <Link to="/login" className="font-display text-2xl font-light text-ink">
                Giriş
              </Link>
              <Link to="/register" className="font-display text-2xl font-light text-sand-deep">
                Kaydol
              </Link>
            </>
          )}
        </nav>
      </div>
    </header>
  );
}
