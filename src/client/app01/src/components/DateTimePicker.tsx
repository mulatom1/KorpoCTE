import React from "react";
import DatePicker, { registerLocale } from "react-datepicker";
import { pl } from "date-fns/locale/pl";
import "react-datepicker/dist/react-datepicker.css";

// Rejestracja polskiej lokalizacji
registerLocale("pl", pl);

interface DateTimePickerProps {
  label: string;
  id: string;
  value: string; // Expected format: yyyy-MM-ddTHH:mm (ISO format)
  onChange: (e: React.ChangeEvent<HTMLInputElement>) => void;
}

const DateTimePicker: React.FC<DateTimePickerProps> = ({
  label,
  id,
  value,
  onChange,
}) => {
  // Convert ISO string to Date object
  const parseValue = (isoValue: string): Date | null => {
    if (!isoValue) return null;
    const date = new Date(isoValue);
    return isNaN(date.getTime()) ? null : date;
  };

  // Convert Date to ISO string (yyyy-MM-ddTHH:mm)
  const formatToISO = (date: Date): string => {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, "0");
    const day = String(date.getDate()).padStart(2, "0");
    const hours = String(date.getHours()).padStart(2, "0");
    const minutes = String(date.getMinutes()).padStart(2, "0");
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  };

  const handleDateChange = (date: Date | null) => {
    if (date) {
      const isoValue = formatToISO(date);
      // Create a synthetic event
      const syntheticEvent = {
        target: {
          id,
          value: isoValue,
        },
      } as React.ChangeEvent<HTMLInputElement>;
      onChange(syntheticEvent);
    }
  };

  return (
    <div>
      <label
        htmlFor={id}
        className="block text-gray-300 text-sm font-medium mb-2"
      >
        {label}
      </label>
      <DatePicker
        id={id}
        selected={parseValue(value)}
        onChange={handleDateChange}
        showTimeSelect
        timeFormat="HH:mm"
        timeIntervals={15}
        dateFormat="yyyy-MM-dd HH:mm"
        locale="pl"
        className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
        wrapperClassName="w-full"
        calendarClassName="dark-calendar"
      />
    </div>
  );
};

export default DateTimePicker;
