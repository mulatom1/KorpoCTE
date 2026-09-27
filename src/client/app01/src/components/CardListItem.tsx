import React from "react";

interface CardListItemProps {
  children: React.ReactNode;
  isVisible?: boolean;
  index?: number;
  delayBase?: number;
  delayStep?: number;
  className?: string;
}

const CardListItem: React.FC<CardListItemProps> = ({
  children,
  isVisible,
  index = 0,
  delayBase = 400,
  delayStep = 50,
  className,
}) => {
  const animationClasses =
    isVisible !== undefined
      ? `${isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-4"}`
      : "";

  return (
    <div
      className={`bg-gray-800/50 backdrop-blur-sm rounded-xl p-5 border border-gray-700/50 hover:border-cyan-500/30 transition-all duration-500 ${animationClasses} ${className || ""}`}
      style={
        isVisible !== undefined
          ? {
              transitionDelay: isVisible
                ? `${delayBase + index * delayStep}ms`
                : "0ms",
            }
          : undefined
      }
    >
      {children}
    </div>
  );
};

export default CardListItem;
