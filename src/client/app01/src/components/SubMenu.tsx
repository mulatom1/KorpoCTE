import { useNavigate, useLocation } from 'react-router';

interface SubMenuItem {
  label: string;
  path: string;
}

interface SubMenuProps {
  backPath: string;
  items: SubMenuItem[];
  isVisible: boolean;
}

function SubMenu({ backPath, items, isVisible }: SubMenuProps) {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <nav
      className={`bg-gray-800/30 backdrop-blur-sm rounded-xl p-2 mb-8 border border-gray-700/30 transition-all duration-700 ease-out delay-200 ${
        isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
      }`}
    >
      <div className="flex flex-wrap items-center gap-1">
        <button
          onClick={() => navigate(backPath)}
          className="flex items-center gap-2 px-4 py-2 rounded-lg text-gray-400 hover:text-white hover:bg-gray-700/50 transition-all duration-200"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          <span>Powrót</span>
        </button>
        <div className="w-px h-6 bg-gray-700/50 mx-1" />
        {items.map((item) => (
          <button
            key={item.path}
            onClick={() => navigate(item.path)}
            className={`px-4 py-2 rounded-lg transition-all duration-200 ${
              location.pathname === item.path
                ? 'bg-cyan-500/20 text-cyan-400 font-medium'
                : 'text-gray-400 hover:text-white hover:bg-gray-700/50'
            }`}
          >
            {item.label}
          </button>
        ))}
      </div>
    </nav>
  );
}

export default SubMenu;
