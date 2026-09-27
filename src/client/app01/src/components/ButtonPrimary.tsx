import React from "react";

interface ButtonPrimaryProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  children: React.ReactNode;
}

const ButtonPrimary: React.FC<ButtonPrimaryProps> = ({
  children,
  ...props
}) => {
  return (
    <button
      {...props}
      className={`px-6 py-2 bg-gradient-to-r from-cyan-500 to-teal-500 text-white font-semibold rounded-xl hover:from-cyan-600 hover:to-teal-600 transition-all duration-200 shadow-lg shadow-cyan-500/25 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 justify-center ${props.className || ""}`}
    >
      {children}
    </button>
  );
};

export default ButtonPrimary;
