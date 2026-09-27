import React from 'react';

interface ListSelectOption {
  value: string;
  label: string;
}

interface ListSelectProps {
  label: React.ReactNode;
  id: string;
  name?: string;
  value: string;
  onChange: (e: React.ChangeEvent<HTMLSelectElement>) => void;
  options: ListSelectOption[];
  placeholder?: string;
}

const ListSelect: React.FC<ListSelectProps> = ({
  label,
  id,
  name,
  value,
  onChange,
  options,
  placeholder,
}) => {
  return (
    <div>
      <label htmlFor={id} className="block text-gray-300 text-sm font-medium mb-2">
        {label}
      </label>
      <select
        id={id}
        name={name ?? id}
        value={value}
        onChange={onChange}
        className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
      >
        {placeholder && <option value="">{placeholder}</option>}
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
    </div>
  );
};

export default ListSelect;
