import { useEffect } from "react";
import { Navigate, Route, Routes, useLocation } from "react-router-dom";
import { Nav } from "@/components/Nav";
import { Footer } from "@/components/Footer";
import { SmoothScroll, useScrollReset } from "@/motion/SmoothScroll";
import { Landing } from "@/pages/Landing";
import { Auctions } from "@/pages/Auctions";
import { AuctionDetail } from "@/pages/AuctionDetail";
import { CreateAuction } from "@/pages/CreateAuction";
import { Login } from "@/pages/Login";
import { Register } from "@/pages/Register";
import { canSell, useAuthStore } from "@/store/authStore";

function RequireAuth({ children, sellerOnly = false }: { children: React.ReactNode; sellerOnly?: boolean }) {
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  const location = useLocation();

  if (status !== "ready") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-paper">
        <p className="font-mono text-eyebrow uppercase text-stone">Oturum kontrol ediliyor…</p>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" state={{ from: location.pathname }} replace />;
  }

  if (sellerOnly && !canSell(user)) {
    return <Navigate to="/auctions" replace />;
  }

  return <>{children}</>;
}

function Shell() {
  const location = useLocation();
  useScrollReset(location.pathname);

  const bare = ["/login", "/register"].includes(location.pathname);

  return (
    <>
      {!bare && <Nav />}
      <main>
        <Routes>
          <Route path="/" element={<Landing />} />
          <Route path="/auctions" element={<Auctions />} />
          <Route
            path="/auctions/new"
            element={
              <RequireAuth sellerOnly>
                <CreateAuction />
              </RequireAuth>
            }
          />
          <Route path="/auctions/:id" element={<AuctionDetail />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
      {!bare && <Footer />}
    </>
  );
}

export function App() {
  const bootstrap = useAuthStore((state) => state.bootstrap);

  useEffect(() => {
    bootstrap();
  }, [bootstrap]);

  return (
    <SmoothScroll>
      <Shell />
    </SmoothScroll>
  );
}
