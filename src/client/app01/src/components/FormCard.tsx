import React from 'react';

interface FormCardProps {
  children: React.ReactNode;
  isVisible: boolean;
  borderColor?: 'green' | 'purple' | 'blue' | 'red' | 'cyan';
  className?: string;
}

const FormCard: React.FC<FormCardProps> = ({
  children,
  isVisible,
  borderColor = 'green',
  className = ''
}) => {
  const borderColorClass = {
    green: 'border-green-500/30',
    purple: 'border-purple-500/30',
    blue: 'border-blue-500/30',
    red: 'border-red-500/30',
    cyan: 'border-cyan-500/30',
  }[borderColor];

  return (
    <div
      className={`bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 mb-8 border ${borderColorClass} transition-all duration-500 ${
        isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
      } ${className}`}
    >
      {children}
    </div>
  );
};

export default FormCard;
