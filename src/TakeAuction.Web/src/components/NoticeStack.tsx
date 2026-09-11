export function NoticeStack({ children }: { children: React.ReactNode }) {
  return (
    <div className="pointer-events-none fixed bottom-6 right-6 z-50 flex w-[min(24rem,calc(100vw-3rem))] flex-col gap-3">
      {children}
    </div>
  );
}
