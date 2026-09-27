import React, { useEffect, useRef, useState } from "react";

interface HLSVideoPlayerProps {
  src: string;
  title?: string;
  poster?: string;
}

export const HLSVideoPlayer: React.FC<HLSVideoPlayerProps> = ({
  src,
  title,
  poster,
}) => {
  const videoRef = useRef<HTMLVideoElement>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    // Sprawdź czy plik istnieje
    const checkVideoExists = async () => {
      try {
        const response = await fetch(src, { method: "HEAD" });
        if (!response.ok) {
          throw new Error("Video file not found");
        }

        // Dla demonstracji używamy natywnego video (HLS wymaga https lub hls.js)
        video.src = src;
        setIsLoading(false);
      } catch (err) {
        setError(
          "Nie można załadować wideo. Plik może nie istnieć lub jest niedostępny.",
        );
        setIsLoading(false);
        console.error("Video load error:", err);
      }
    };

    checkVideoExists();
  }, [src]);

  // Zabezpieczenia anty-kopiowanie
  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;

    const handleContextMenu = (e: MouseEvent) => {
      e.preventDefault();
      return false;
    };

    const handleKeyDown = (e: KeyboardEvent) => {
      if (
        (e.ctrlKey && e.key === "s") ||
        (e.ctrlKey && e.shiftKey && e.key === "I") ||
        e.key === "F12"
      ) {
        e.preventDefault();
        return false;
      }
    };

    const handleDragStart = (e: DragEvent) => {
      e.preventDefault();
      return false;
    };

    video.addEventListener("contextmenu", handleContextMenu);
    video.addEventListener("keydown", handleKeyDown);
    video.addEventListener("dragstart", handleDragStart);

    return () => {
      video.removeEventListener("contextmenu", handleContextMenu);
      video.removeEventListener("keydown", handleKeyDown);
      video.removeEventListener("dragstart", handleDragStart);
    };
  }, []);

  if (error) {
    return (
      <div className="video-error bg-red-900/20 border border-red-500/30 rounded-lg p-6 my-4">
        <p className="text-red-400 text-center">{error}</p>
      </div>
    );
  }

  return (
    <div className="hls-video-container relative my-8 rounded-lg overflow-hidden bg-black shadow-xl">
      {isLoading && (
        <div className="video-loading flex flex-col items-center justify-center min-h-[400px] bg-gray-900">
          <div className="spinner border-4 border-gray-700 border-t-cyan-500 rounded-full w-12 h-12 animate-spin mb-4"></div>
          <p className="text-gray-400">Ładowanie wideo...</p>
        </div>
      )}
      <video
        ref={videoRef}
        controls
        controlsList="nodownload noremoteplayback"
        disablePictureInPicture
        poster={poster}
        className="hls-video-player w-full max-h-[600px] select-none"
        style={{ display: isLoading ? "none" : "block" }}
      >
        Twoja przeglądarka nie wspiera odtwarzania wideo.
      </video>
      {title && (
        <p className="video-title bg-gray-900 text-gray-300 px-4 py-2 text-sm">
          {title}
        </p>
      )}
      <div className="video-watermark absolute bottom-16 right-5 text-white/30 text-xs font-semibold pointer-events-none select-none">
        © Tomsoft1 - Materiał chroniony
      </div>
    </div>
  );
};
