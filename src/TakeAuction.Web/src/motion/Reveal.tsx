import { useInView } from "@/lib/hooks";

interface RevealProps {
  children: React.ReactNode;
  delay?: number;
  className?: string;
  as?: "div" | "section" | "li" | "span";
}

/** Fade-and-rise on first entry. Kept CSS-only so it costs nothing on the main thread. */
export function Reveal({ children, delay = 0, className = "", as = "div" }: RevealProps) {
  const { ref, inView } = useInView<HTMLDivElement>();
  const Tag = as;

  return (
    <Tag
      ref={ref as never}
      className={`transition-[opacity,transform] duration-[1100ms] ease-editorial ${
        inView ? "translate-y-0 opacity-100" : "translate-y-8 opacity-0"
      } ${className}`}
      style={{ transitionDelay: `${delay}ms` }}
    >
      {children}
    </Tag>
  );
}

interface SplitLineProps {
  text: string;
  className?: string;
  delay?: number;
}

/** Word-by-word mask reveal for headline type. */
export function SplitLine({ text, className = "", delay = 0 }: SplitLineProps) {
  const { ref, inView } = useInView<HTMLSpanElement>();
  const words = text.split(" ");

  // No display utility of its own: callers stack lines by passing `block`, and a
  // hardcoded `inline-block` here would win on stylesheet order and run them together.
  return (
    <span ref={ref} className={className}>
      {words.map((word, index) => (
        <span key={`${word}-${index}`} className="inline-block overflow-hidden align-bottom">
          <span
            className="inline-block transition-transform duration-[1000ms] ease-editorial"
            style={{
              transform: inView ? "translateY(0)" : "translateY(110%)",
              transitionDelay: `${delay + index * 55}ms`,
            }}
          >
            {word}
            {index < words.length - 1 ? " " : ""}
          </span>
        </span>
      ))}
    </span>
  );
}
