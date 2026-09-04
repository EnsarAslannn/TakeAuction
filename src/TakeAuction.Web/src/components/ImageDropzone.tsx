import { useId, useRef, useState } from "react";
import { ACCEPTED_IMAGE_TYPES, MAX_IMAGE_SIZE_BYTES } from "@/api/auctions";
import { useT } from "@/i18n";

const MAX_MB = Math.round(MAX_IMAGE_SIZE_BYTES / (1024 * 1024));

interface ImageDropzoneProps {
  preview: string | null;
  uploading: boolean;
  error: string | null;
  onSelect: (file: File) => void;
  onClear: () => void;
}

export function ImageDropzone({
  preview,
  uploading,
  error,
  onSelect,
  onClear,
}: ImageDropzoneProps) {
  const inputId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const t = useT();

  const take = (files: FileList | null) => {
    const file = files?.[0];
    if (file) onSelect(file);
  };

  if (preview) {
    return (
      <div className="space-y-4">
        <div className="relative aspect-[4/3] overflow-hidden bg-ink">
          <div
            aria-hidden
            className="absolute inset-0 scale-110 bg-cover bg-center opacity-25 blur-2xl"
            style={{ backgroundImage: `url(${preview})` }}
          />
          <img
            src={preview}
            alt={t("dropzone.uploadedAlt")}
            className="absolute inset-0 h-full w-full object-contain p-5"
          />

          {uploading && (
            <div className="absolute inset-0 flex items-center justify-center bg-ink/55">
              <span className="font-mono text-eyebrow uppercase text-paper/70">
                {t("dropzone.uploading")}
              </span>
            </div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-x-6 gap-y-2">
          <button
            type="button"
            onClick={() => inputRef.current?.click()}
            className="font-mono text-eyebrow uppercase text-stone underline underline-offset-4 transition-colors hover:text-ink"
          >
            {t("dropzone.replace")}
          </button>
          <button
            type="button"
            onClick={onClear}
            className="font-mono text-eyebrow uppercase text-stone underline underline-offset-4 transition-colors hover:text-sand-deep"
          >
            {t("dropzone.remove")}
          </button>
        </div>

        <input
          ref={inputRef}
          id={inputId}
          type="file"
          accept={ACCEPTED_IMAGE_TYPES.join(",")}
          className="hidden"
          onChange={(event) => {
            take(event.target.files);
            event.target.value = "";
          }}
        />

        {error && <p className="font-sans text-xs text-sand-deep">{error}</p>}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <label
        htmlFor={inputId}
        onDragOver={(event) => {
          event.preventDefault();
          setOver(true);
        }}
        onDragLeave={() => setOver(false)}
        onDrop={(event) => {
          event.preventDefault();
          setOver(false);
          take(event.dataTransfer.files);
        }}
        className={`flex aspect-[4/3] cursor-pointer flex-col items-center justify-center gap-4 border border-dashed px-8 text-center transition-colors duration-500 ${
          over ? "border-sand bg-sand/8" : "border-ink/20 hover:border-ink/40 hover:bg-paper-pure"
        }`}
      >
        <span aria-hidden className="font-display text-3xl font-light text-ink/25">
          +
        </span>
        <span className="font-mono text-eyebrow uppercase text-stone">
          {uploading ? t("dropzone.uploading") : t("dropzone.prompt")}
        </span>
        <span className="max-w-[34ch] font-sans text-xs leading-relaxed text-ink/45">
          {t("dropzone.hint", { mb: MAX_MB })}
        </span>
      </label>

      <input
        ref={inputRef}
        id={inputId}
        type="file"
        accept={ACCEPTED_IMAGE_TYPES.join(",")}
        className="hidden"
        onChange={(event) => {
          take(event.target.files);
          event.target.value = "";
        }}
      />

      {error && <p className="font-sans text-xs text-sand-deep">{error}</p>}
    </div>
  );
}
