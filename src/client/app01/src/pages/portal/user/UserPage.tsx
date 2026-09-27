import { useEffect, useState, useCallback } from 'react';
import { ApiPortalService as ApiService } from '../../../services/api-portal-service';
import { getIsAdminFromToken } from '../../../utils/jwt';
import type { UserDto } from '../../../services/contracts/user-list-response';
import TextEdit from '../../../components/TextEdit';

function UserPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [users, setUsers] = useState<UserDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');
  const [updatingUserId, setUpdatingUserId] = useState<number | null>(null);
  const [deletingUserId, setDeletingUserId] = useState<number | null>(null);
  const [resettingUserId, setResettingUserId] = useState<number | null>(null);
  const currentUserEmail = localStorage.getItem('userEmail');

  const [filters, setFilters] = useState({
    email: '',
    isAdmin: '' as '' | 'true' | 'false',
    page: 1,
    pageSize: 10,
  });

  const isAdmin = getIsAdminFromToken();
  const token = localStorage.getItem('token');

  const fetchUsers = useCallback(async () => {
    // Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
    if (!isAdmin) {
      setError('Brak uprawnien. Tylko administratorzy maja dostep do tej strony.');
      return;
    }

    setIsLoading(true);
    setError('');

    try {
      const apiService = new ApiService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiService.setUsrToken(token ?? '');

      const response = await apiService.userList({
        email: filters.email || undefined,
        isAdmin: filters.isAdmin === '' ? undefined : filters.isAdmin === 'true',
        page: filters.page,
        pageSize: filters.pageSize,
      });

      setUsers(response.users);
      setTotalCount(response.totalCount);
      setTotalPages(response.totalPages);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd pobierania listy użytkowników');
    } finally {
      setIsLoading(false);
    }
  }, [token, isAdmin, filters]);

  useEffect(() => {
    document.title = 'Użytkownicy | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    fetchUsers();
  }, [fetchUsers]);

  const handleFilterChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const { name, value } = e.target;
    setFilters(prev => ({
      ...prev,
      [name]: value,
      page: 1,
    }));
  };

  const handleEmailChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setFilters(prev => ({
      ...prev,
      [name]: value,
      page: 1,
    }));
  };

  const handlePageChange = (newPage: number) => {
    setFilters(prev => ({
      ...prev,
      page: newPage,
    }));
  };

  const handleToggleAdmin = async (user: UserDto) => {
    if (!token) return;

    setUpdatingUserId(user.id);
    setError('');

    try {
      const apiService = new ApiService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiService.setUsrToken(token);

      await apiService.userSet({ email: user.email });
      await fetchUsers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd aktualizacji użytkownika');
    } finally {
      setUpdatingUserId(null);
    }
  };

  const handleResetPassword = async (user: UserDto) => {
    if (!token) return;

    if (!confirm(`Czy na pewno chcesz zresetować hasło użytkownika ${user.email} na "BrakBrak"?`)) {
      return;
    }

    setResettingUserId(user.id);
    setError('');

    try {
      const apiService = new ApiService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiService.setUsrToken(token);

      await apiService.userPassReset({ email: user.email });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd resetowania hasła');
    } finally {
      setResettingUserId(null);
    }
  };

  const handleDeleteUser = async (user: UserDto) => {
    if (!token) return;

    if (!confirm(`Czy na pewno chcesz usunąć użytkownika ${user.email}?`)) {
      return;
    }

    setDeletingUserId(user.id);
    setError('');

    try {
      const apiService = new ApiService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );
      apiService.setUsrToken(token);

      await apiService.userDelete({ email: user.email });
      await fetchUsers();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd usuwania użytkownika');
    } finally {
      setDeletingUserId(null);
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}, ${hours}:${minutes}`;
  };

  if (!token) {
    return null;
  }

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-8">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Użytkownicy
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Zarządzanie użytkownikami systemu
          </p>
        </div>

        <div
          className={`transition-all duration-700 ease-out ${
            isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
          }`}
          style={{ transitionDelay: isVisible ? '300ms' : '0ms' }}
        >
          {/* Filters */}
          <div className="bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 mb-6">
            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
              <TextEdit
                label="Email"
                id="email"
                name="email"
                type="text"
                value={filters.email}
                onChange={handleEmailChange}
                placeholder="Szukaj po email..."
              />
              <div>
                <label htmlFor="isAdmin" className="block text-gray-300 text-sm font-medium mb-2">
                  Rola
                </label>
                <select
                  id="isAdmin"
                  name="isAdmin"
                  value={filters.isAdmin}
                  onChange={handleFilterChange}
                  className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
                >
                  <option value="">Wszyscy</option>
                  <option value="true">Administratorzy</option>
                  <option value="false">Użytkownicy</option>
                </select>
              </div>
              <div>
                <label htmlFor="pageSize" className="block text-gray-300 text-sm font-medium mb-2">
                  Na stronie
                </label>
                <select
                  id="pageSize"
                  name="pageSize"
                  value={filters.pageSize}
                  onChange={handleFilterChange}
                  className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
                >
                  <option value="5">5</option>
                  <option value="10">10</option>
                  <option value="25">25</option>
                  <option value="50">50</option>
                </select>
              </div>
            </div>
          </div>

          {/* Error message */}
          {error && (
            <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
              {error}
            </div>
          )}

          {/* Table */}
          <div className="bg-gray-800/50 backdrop-blur-sm rounded-2xl border border-gray-700/50 overflow-hidden">
            {isLoading ? (
              <div className="p-8 text-center text-gray-400">Ładowanie...</div>
            ) : users.length === 0 ? (
              <div className="p-8 text-center text-gray-400">Brak użytkowników</div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full">
                  <thead className="bg-gray-900/50">
                    <tr>
                      <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">
                        ID
                      </th>
                      <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">
                        Email
                      </th>
                      <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">
                        Rola
                      </th>
                      <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">
                        Data utworzenia
                      </th>
                      <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">
                        Akcje
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-700/50">
                    {users.map((user) => (
                      <tr key={user.id} className="hover:bg-gray-700/30 transition-colors">
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-300">
                          {user.id}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-white">
                          {user.email}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <span
                            className={`inline-flex px-2 py-1 text-xs font-semibold rounded-full ${
                              user.isAdmin
                                ? 'bg-purple-500/20 text-purple-400 border border-purple-500/50'
                                : 'bg-gray-500/20 text-gray-400 border border-gray-500/50'
                            }`}
                          >
                            {user.isAdmin ? 'Admin' : 'User'}
                          </span>
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-400">
                          {formatDate(user.createdAt)}
                        </td>
                        <td className="px-6 py-4 whitespace-nowrap">
                          <div className="flex gap-2">
                            <button
                              onClick={() => handleToggleAdmin(user)}
                              disabled={updatingUserId === user.id || deletingUserId === user.id}
                              className={`px-3 py-1.5 text-xs font-medium rounded-lg transition-all duration-200 ${
                                user.isAdmin
                                  ? 'bg-orange-500/20 text-orange-400 border border-orange-500/50 hover:bg-orange-500/30'
                                  : 'bg-green-500/20 text-green-400 border border-green-500/50 hover:bg-green-500/30'
                              } disabled:opacity-50 disabled:cursor-not-allowed`}
                            >
                              {updatingUserId === user.id
                                ? 'Aktualizacja...'
                                : user.isAdmin
                                ? 'Odbierz admina'
                                : 'Nadaj admina'}
                            </button>
                            <button
                              onClick={() => handleResetPassword(user)}
                              disabled={resettingUserId === user.id || deletingUserId === user.id || updatingUserId === user.id}
                              className="px-3 py-1.5 text-xs font-medium rounded-lg transition-all duration-200 bg-yellow-500/20 text-yellow-400 border border-yellow-500/50 hover:bg-yellow-500/30 disabled:opacity-50 disabled:cursor-not-allowed"
                              title="Resetuj hasło do BrakBrak"
                            >
                              {resettingUserId === user.id ? 'Resetowanie...' : 'Resetuj hasło'}
                            </button>
                            <button
                              onClick={() => handleDeleteUser(user)}
                              disabled={deletingUserId === user.id || updatingUserId === user.id || resettingUserId === user.id || user.email === currentUserEmail}
                              className="px-3 py-1.5 text-xs font-medium rounded-lg transition-all duration-200 bg-red-500/20 text-red-400 border border-red-500/50 hover:bg-red-500/30 disabled:opacity-50 disabled:cursor-not-allowed"
                              title={user.email === currentUserEmail ? 'Nie możesz usunąć swojego konta' : 'Usuń użytkownika'}
                            >
                              {deletingUserId === user.id ? 'Usuwanie...' : 'Usuń'}
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="px-6 py-4 border-t border-gray-700/50 flex items-center justify-between">
                <div className="text-sm text-gray-400">
                  Wyświetlono {users.length} z {totalCount} użytkowników
                </div>
                <div className="flex gap-2">
                  <button
                    onClick={() => handlePageChange(filters.page - 1)}
                    disabled={filters.page <= 1}
                    className="px-3 py-1.5 text-sm bg-gray-700/50 text-gray-300 rounded-lg hover:bg-gray-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    Poprzednia
                  </button>
                  <span className="px-3 py-1.5 text-sm text-gray-400">
                    Strona {filters.page} z {totalPages}
                  </span>
                  <button
                    onClick={() => handlePageChange(filters.page + 1)}
                    disabled={filters.page >= totalPages}
                    className="px-3 py-1.5 text-sm bg-gray-700/50 text-gray-300 rounded-lg hover:bg-gray-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                  >
                    Nastepna
                  </button>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </section>
  );
}

export default UserPage;
