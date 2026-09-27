import React from 'react';

interface ButtonSecondaryProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  children: React.ReactNode;
}

const ButtonSecondary: React.FC<ButtonSecondaryProps> = ({ children, ...props }) => {
  return (
    <button
      {...props}
      className={`px-6 py-2 bg-gradient-to-r from-slate-500 to-gray-500 text-white font-semibold rounded-xl hover:from-slate-600 hover:to-gray-600 transition-all duration-200 shadow-lg shadow-slate-500/25 disabled:opacity-50 disabled:cursor-not-allowed flex items-center gap-2 justify-center ${props.className || ''}`}
    >
      {children}
    </button>
  );
};

export default ButtonSecondary;