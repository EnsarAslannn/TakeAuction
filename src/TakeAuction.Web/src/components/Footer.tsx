import { Link } from "react-router-dom";
import { VISUALS } from "@/content/catalog";

const STACK = [
  ".NET 10 Minimal API",
  "Vertical Slice + CQRS",
  "PostgreSQL",
  "Redis",
  "RabbitMQ",
  "Hangfire",
  "SignalR",
  "React + Three.js",
];

export function Footer() {
  return (
    <footer data-nav-theme="dark" className="relative overflow-hidden bg-ink text-paper">
      <div className="absolute inset-0">
        <img
          src={VISUALS.plaster}
          alt=""
          aria-hidden
          loading="lazy"
          className="h-full w-full object-cover opacity-[0.12]"
        />
      </div>

      <div className="shell relative z-10 mx-auto max-w-shell py-24 md:py-32">
        <div className="flex flex-col gap-14 md:flex-row md:items-end md:justify-between">
          <div>
            <p className="eyebrow mb-8 text-paper/40">Salona gir</p>
            <h2 className="font-display text-giant font-light leading-[0.88]">
              take<span className="text-sand">auction</span>
            </h2>
          </div>

          <div className="flex flex-wrap gap-3">
            <Link
              to="/auctions"
              className="btn border border-paper/25 text-paper hover:border-sand hover:bg-sand hover:text-ink"
            >
              Açık artırmalar
            </Link>
            <Link to="/register" className="btn bg-paper text-ink hover:bg-sand">
              Hesap aç
            </Link>
          </div>
        </div>

        <div className="mt-20 border-t border-paper/12 pt-10">
          <div className="flex flex-col gap-8 md:flex-row md:items-start md:justify-between">
            <ul className="flex max-w-[46rem] flex-wrap gap-x-6 gap-y-3">
              {STACK.map((item) => (
                <li key={item} className="font-mono text-eyebrow uppercase text-paper/35">
                  {item}
                </li>
              ))}
            </ul>

            <p className="font-mono text-eyebrow uppercase text-paper/35">
              © {new Date().getFullYear()} TakeAuction
            </p>
          </div>
        </div>
      </div>
    </footer>
  );
}
