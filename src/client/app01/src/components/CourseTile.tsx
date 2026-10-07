import { useState } from "react";
import CardListItem from "./CardListItem";
import type { CourseTileDto } from "../services/contracts/courses-course-tiles-response";

interface CourseTileProps {
  tile: CourseTileDto;
  apiUrl: string;
  index: number;
  isVisible: boolean;
}

// Kafelek kursu – celowo nieklikalny (bez linku i onClick).
function CourseTile({ tile, apiUrl, index, isVisible }: CourseTileProps) {
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = tile.imageUrl !== null && !imageFailed;

  return (
    <CardListItem
      isVisible={isVisible}
      index={index}
      delayBase={300}
      delayStep={100}
      className="w-full md:w-80 p-6 rounded-2xl"
    >
      <div className="w-full h-40 rounded-xl overflow-hidden mb-4 bg-gray-900/50">
        {showImage ? (
          <img
            src={`${apiUrl}${tile.imageUrl}`}
            alt={tile.title}
            className="w-full h-full object-contain"
            onError={() => setImageFailed(true)}
          />
        ) : (
          <div
            data-testid="course-tile-placeholder"
            aria-hidden="true"
            className="w-full h-full flex items-center justify-center text-gray-600 text-4xl font-bold"
          >
            {tile.title.charAt(0).toUpperCase()}
          </div>
        )}
      </div>
      <h3 className="text-white font-semibold text-lg mb-2 text-center">
        {tile.title}
      </h3>
      <p className="text-gray-400 text-sm text-center mb-4">
        {tile.shortDescription}
      </p>
      {tile.tags.length > 0 && (
        <ul className="flex flex-wrap justify-center gap-2">
          {tile.tags.map((tag) => (
            <li
              key={tag}
              className="px-2 py-1 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/50"
            >
              {tag}
            </li>
          ))}
        </ul>
      )}
    </CardListItem>
  );
}

export default CourseTile;
