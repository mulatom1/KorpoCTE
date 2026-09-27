import React from "react";

interface CardProps {
  children: React.ReactNode;
  isVisible?: boolean;
  className?: string;
}

const Card: React.FC<CardProps> = ({ children, isVisible, className }) => {
  const animationClasses =
    isVisible !== undefined
      ? `transition-all duration-700 ease-out ${isVisible ? "opacity-100 translate-y-0" : "opacity-0 translate-y-8"}`
      : "";

  return (
    <div
      className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 ${animationClasses} ${className || ""}`}
    >
      {children}
    </div>
  );
};

export default Card;
