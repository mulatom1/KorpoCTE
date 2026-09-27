import React from 'react';

interface TextEditProps {
  label: string;
  id: string;
  name: string;
  type?: 'email' | 'password' | 'text' | 'textarea';
  value: string;
  onChange: (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => void;
  placeholder?: string;
  required?: boolean;
  error?: string;
  rows?: number;
  maxLength?: number;
  minLength?: number;
  className?: string;
}

const TextEdit: React.FC<TextEditProps> = ({
  label,
  id,
  name,
  type = 'text',
  value,
  onChange,
  placeholder = '',
  required = false,
  error,
  rows = 1,
  maxLength,
  minLength,
  className = ''
}) => {
  if (type === 'textarea') {
    return (
      <div className={`mb-4 flex flex-col ${className}`}>
        <label htmlFor={id} className="block text-gray-300 text-sm font-medium mb-2">
          {label}
        </label>
        <textarea
          id={id}
          name={name}
          value={value}
          onChange={onChange}
          required={required}
          rows={rows}
          maxLength={maxLength}
          minLength={minLength}
          className={`w-full flex-1 min-h-0 px-4 py-2 bg-gray-900/50 border rounded-xl text-white placeholder-gray-500 focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors resize-none ${
            error ? 'border-red-500' : 'border-gray-700'
          }`}
          placeholder={placeholder}
        />
        {error && <p className="mt-1 text-red-400 text-sm break-words">{error}</p>}
      </div>
    );
  }

  return (
    <div className={`mb-4 ${className}`}>
      <label htmlFor={id} className="block text-gray-300 text-sm font-medium mb-2">
        {label}
      </label>
      <input
        type={type}
        id={id}
        name={name}
        value={value}
        onChange={onChange}
        required={required}
        maxLength={maxLength}
        minLength={minLength}
        className={`w-full px-4 py-2 bg-gray-900/50 border rounded-xl text-white placeholder-gray-500 focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors ${
          error ? 'border-red-500' : 'border-gray-700'
        }`}
        placeholder={placeholder}
      />
      {error && <p className="mt-1 text-red-400 text-sm">{error}</p>}
    </div>
  );
};

export default TextEdit;