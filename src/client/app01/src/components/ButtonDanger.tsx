import React from 'react';

interface ButtonDangerProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  children: React.ReactNode;
}

const ButtonDanger: React.FC<ButtonDangerProps> = ({ children, ...props }) => {
  return (
    <button
      {...props}
      className={`px-6 py-2 bg-gradient-to-r from-red-600 to-red-700 text-white font-semibold rounded-xl hover:from-red-700 hover:to-red-800 transition-all duration-200 shadow-lg shadow-red-500/25 disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-2 ${props.className || ''}`}
    >
      {children}
    </button>
  );
};

export default ButtonDanger;
