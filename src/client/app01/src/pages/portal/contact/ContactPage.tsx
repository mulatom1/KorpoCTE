import { useEffect, useState, useCallback } from 'react';
import { ApiPortalService } from '../../../services/api-portal-service';
import { getIsAdminFromToken } from '../../../utils/jwt';
import type { MailListDto } from '../../../services/contracts/mail-from-client-list-response';
import type { MailFromClientGetResponse } from '../../../services/contracts/mail-from-client-get-response';
import TextEdit from '../../../components/TextEdit';
import ButtonPrimary from '../../../components/ButtonPrimary';
import ListSelect from '../../../components/ListSelect';

function ContactPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitStatus, setSubmitStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');

  const isAdmin = getIsAdminFromToken();
  const token = localStorage.getItem('token');

  // Admin — mail list state
  const [mails, setMails] = useState<MailListDto[]>([]);
  const [mailsTotal, setMailsTotal] = useState(0);
  const [mailsTotalPages, setMailsTotalPages] = useState(0);
  const [mailsPage, setMailsPage] = useState(1);
  const [mailsLoading, setMailsLoading] = useState(false);
  const [mailsError, setMailsError] = useState('');
  const [selectedMail, setSelectedMail] = useState<MailFromClientGetResponse | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [loadingMailId, setLoadingMailId] = useState<number | null>(null);

  useEffect(() => {
    document.title = 'Kontakt | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const [formData, setFormData] = useState({
    name: '',
    email: '',
    subject: '',
    message: '',
  });

  const fetchMails = useCallback(async (page: number) => {
    if (!token || !isAdmin) return;

    setMailsLoading(true);
    setMailsError('');

    try {
      const api = new ApiPortalService(import.meta.env.VITE_API_URL, import.meta.env.VITE_APP_TOKEN);
      api.setUsrToken(token);

      const result = await api.mailFromClientList({ page, pageSize: 10 });
      setMails(result.mails);
      setMailsTotal(result.totalCount);
      setMailsTotalPages(result.totalPages);
    } catch (err) {
      setMailsError(err instanceof Error ? err.message : 'Błąd pobierania wiadomości');
    } finally {
      setMailsLoading(false);
    }
  }, [token, isAdmin]);

  useEffect(() => {
    if (isAdmin && token) {
      fetchMails(mailsPage);
    }
  }, [isAdmin, token, mailsPage, fetchMails]);

  const handleMailClick = async (id: number) => {
    if (!token) return;

    setLoadingMailId(id);
    try {
      const api = new ApiPortalService(import.meta.env.VITE_API_URL, import.meta.env.VITE_APP_TOKEN);
      api.setUsrToken(token);

      const result = await api.mailFromClientGet({ id });
      setSelectedMail(result);
      setModalOpen(true);
    } catch (err) {
      setMailsError(err instanceof Error ? err.message : 'Błąd pobierania wiadomości');
    } finally {
      setLoadingMailId(null);
    }
  };

  const handleDeleteMail = async (id: number) => {
    if (!token) return;
    if (!confirm('Czy na pewno chcesz usunąć tę wiadomość?')) return;

    setDeletingId(id);
    try {
      const api = new ApiPortalService(import.meta.env.VITE_API_URL, import.meta.env.VITE_APP_TOKEN);
      api.setUsrToken(token);

      await api.mailFromClientDelete({ id });
      setModalOpen(false);
      setSelectedMail(null);
      fetchMails(mailsPage);
    } catch (err) {
      setMailsError(err instanceof Error ? err.message : 'Błąd usuwania wiadomości');
    } finally {
      setDeletingId(null);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setSubmitStatus('idle');
    setErrorMessage('');

    try {
      const apiService = new ApiPortalService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );

      await apiService.mailFromClientAdd({
        email: formData.email,
        topic: `[${formData.subject}] ${formData.name}`,
        body: formData.message,
      });

      setSubmitStatus('success');
      setFormData({ name: '', email: '', subject: '', message: '' });
    } catch (error) {
      setSubmitStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Wystąpił błąd podczas wysyłania wiadomości');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
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

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-4xl mx-auto w-full">
        {/* Nagłówek strony */}
        <div className="text-center mb-12">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Kontakt
          </h1>
          <p
            className={`text-gray-400 text-lg max-w-2xl mx-auto transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Masz pytanie, propozycję współpracy lub potrzebujesz konsultacji? Napisz do mnie!
          </p>
        </div>

        <div className="flex flex-col md:flex-row gap-8">
          {/* Informacje kontaktowe */}
          <div
            className={`md:w-1/3 space-y-4 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 -translate-x-8'
            }`}
            style={{ transitionDelay: isVisible ? '300ms' : '0ms' }}
          >
            {/* Email */}
            <div className="bg-gray-800/50 backdrop-blur-sm rounded-xl p-5 border border-gray-700/50">
              <div className="flex items-center gap-3 mb-2">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                  <svg className="w-5 h-5 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3 8l7.89 5.26a2 2 0 002.22 0L21 8M5 19h14a2 2 0 002-2V7a2 2 0 00-2-2H5a2 2 0 00-2 2v10a2 2 0 002 2z" />
                  </svg>
                </div>
                <span className="text-white font-medium">Email</span>
              </div>
              <p className="text-gray-400 text-sm">contact@tomsoft1.pl</p>
            </div>

            {/* Lokalizacja */}
            <div className="bg-gray-800/50 backdrop-blur-sm rounded-xl p-5 border border-gray-700/50">
              <div className="flex items-center gap-3 mb-2">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                  <svg className="w-5 h-5 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
                  </svg>
                </div>
                <span className="text-white font-medium">Lokalizacja</span>
              </div>
              <p className="text-gray-400 text-sm">Polska</p>
            </div>

            {/* Dostępność */}
            <div className="bg-gray-800/50 backdrop-blur-sm rounded-xl p-5 border border-gray-700/50">
              <div className="flex items-center gap-3 mb-2">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center">
                  <svg className="w-5 h-5 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                  </svg>
                </div>
                <span className="text-white font-medium">Dostępność</span>
              </div>
              <p className="text-gray-400 text-sm">Otwarty na nowe projekty</p>
            </div>

            {/* CV View */}
            <a
              href="/my-cv"
              className="block bg-gray-800/50 backdrop-blur-sm rounded-xl p-5 border border-gray-700/50 hover:border-cyan-500/30 transition-all duration-300 group"
            >
              <div className="flex items-center gap-3 mb-2">
                <div className="w-10 h-10 rounded-lg bg-cyan-500/20 flex items-center justify-center group-hover:scale-110 transition-transform">
                  <svg className="w-5 h-5 text-cyan-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                  </svg>
                </div>
                <span className="text-white font-medium">Moje CV</span>
              </div>
              <p className="text-cyan-400 text-sm">Wyświetl moje CV</p>
            </a>

            {/* Social Links */}
            <div className="flex gap-3">
              {/* GitHub */}
              <a
                href="https://github.com/mulatom1"
                target="_blank"
                rel="noopener noreferrer"
                className="flex-1 bg-gray-800/50 backdrop-blur-sm rounded-xl p-4 border border-gray-700/50 hover:border-blue-400/50 transition-all duration-300 group flex flex-col items-center"
              >
                <div className="w-10 h-10 rounded-lg bg-blue-500/20 flex items-center justify-center group-hover:scale-110 transition-transform mb-2">
                  <svg className="w-5 h-5 text-blue-500" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M12 0c-6.626 0-12 5.373-12 12 0 5.302 3.438 9.8 8.207 11.387.599.111.793-.261.793-.577v-2.234c-3.338.726-4.033-1.416-4.033-1.416-.546-1.387-1.333-1.756-1.333-1.756-1.089-.745.083-.729.083-.729 1.205.084 1.839 1.237 1.839 1.237 1.07 1.834 2.807 1.304 3.492.997.107-.775.418-1.305.762-1.604-2.665-.305-5.467-1.334-5.467-5.931 0-1.311.469-2.381 1.236-3.221-.124-.303-.535-1.524.117-3.176 0 0 1.008-.322 3.301 1.23.957-.266 1.983-.399 3.003-.404 1.02.005 2.047.138 3.006.404 2.291-1.552 3.297-1.23 3.297-1.23.653 1.653.242 2.874.118 3.176.77.84 1.235 1.911 1.235 3.221 0 4.609-2.807 5.624-5.479 5.921.43.372.823 1.102.823 2.222v3.293c0 .319.192.694.801.576 4.765-1.589 8.199-6.086 8.199-11.386 0-6.627-5.373-12-12-12z"/>
                  </svg>
                </div>
                <span className="text-gray-400 text-xs">GitHub</span>
              </a>

              {/* LinkedIn */}
              <a
                href="https://www.linkedin.com/in/tomasz-mularczyk-b744b5265/"
                target="_blank"
                rel="noopener noreferrer"
                className="flex-1 bg-gray-800/50 backdrop-blur-sm rounded-xl p-4 border border-gray-700/50 hover:border-blue-400/50 transition-all duration-300 group flex flex-col items-center"
              >
                <div className="w-10 h-10 rounded-lg bg-blue-500/20 flex items-center justify-center group-hover:scale-110 transition-transform mb-2">
                  <svg className="w-5 h-5 text-blue-500" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M20.447 20.452h-3.554v-5.569c0-1.328-.027-3.037-1.852-3.037-1.853 0-2.136 1.445-2.136 2.939v5.667H9.351V9h3.414v1.561h.046c.477-.9 1.637-1.85 3.37-1.85 3.601 0 4.267 2.37 4.267 5.455v6.286zM5.337 7.433c-1.144 0-2.063-.926-2.063-2.065 0-1.138.92-2.063 2.063-2.063 1.14 0 2.064.925 2.064 2.063 0 1.139-.925 2.065-2.064 2.065zm1.782 13.019H3.555V9h3.564v11.452zM22.225 0H1.771C.792 0 0 .774 0 1.729v20.542C0 23.227.792 24 1.771 24h20.451C23.2 24 24 23.227 24 22.271V1.729C24 .774 23.2 0 22.222 0h.003z"/>
                  </svg>
                </div>
                <span className="text-gray-400 text-xs">LinkedIn</span>
              </a>

              {/* Facebook */}
              <a
                href="https://www.facebook.com/profile.php?id=100087925249315"
                target="_blank"
                rel="noopener noreferrer"
                className="flex-1 bg-gray-800/50 backdrop-blur-sm rounded-xl p-4 border border-gray-700/50 hover:border-blue-400/50 transition-all duration-300 group flex flex-col items-center"
              >
                <div className="w-10 h-10 rounded-lg bg-blue-500/20 flex items-center justify-center group-hover:scale-110 transition-transform mb-2">
                  <svg className="w-5 h-5 text-blue-500" fill="currentColor" viewBox="0 0 24 24">
                    <path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/>
                  </svg>
                </div>
                <span className="text-gray-400 text-xs">Facebook</span>
              </a>
            </div>
          </div>

          {/* Formularz kontaktowy */}
          <div
            className={`md:w-2/3 flex transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-x-0' : 'opacity-0 translate-x-8'
            }`}
            style={{ transitionDelay: isVisible ? '400ms' : '0ms' }}
          >
            <form onSubmit={handleSubmit} className="bg-gray-800/50 backdrop-blur-sm rounded-2xl p-6 border border-gray-700/50 w-full flex flex-col">
              <div className="grid sm:grid-cols-2 gap-4 mb-4">
                <TextEdit
                  label="Imię i nazwisko"
                  id="name"
                  name="name"
                  type="text"
                  value={formData.name}
                  onChange={handleChange}
                  placeholder="Jan Kowalski"
                  required
                />
                <TextEdit
                  label="Email"
                  id="email"
                  name="email"
                  type="email"
                  value={formData.email}
                  onChange={handleChange}
                  placeholder="jan@example.com"
                  required
                />
              </div>

              <div className="mb-4">
                <ListSelect
                  label="Temat"
                  id="subject"
                  value={formData.subject}
                  onChange={handleChange}
                  options={[
                    { value: 'kurs', label: 'Dostęp do szkolenia czy aplikacji' },
                    { value: 'projekt', label: 'Zlecenie projektu' },
                    { value: 'konsultacja', label: 'Konsultacja' },
                    { value: 'wspolpraca', label: 'Propozycja współpracy' },
                    { value: 'inne', label: 'Inne' },
                  ]}
                  placeholder="Wybierz temat..."
                />
              </div>

              <TextEdit
                label="Wiadomość"
                id="message"
                name="message"
                type="textarea"
                value={formData.message}
                onChange={handleChange}
                placeholder="Opisz propozycję..."
                required
                className="flex-1"
              />

              {submitStatus === 'success' && (
                <div className="mb-4 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
                  Wiadomość została wysłana pomyślnie!
                </div>
              )}

              {submitStatus === 'error' && (
                <div className="mb-4 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
                  {errorMessage}
                </div>
              )}

              <ButtonPrimary className="w-full py-3" type="submit" disabled={isSubmitting}>
                {isSubmitting ? 'Wysyłanie...' : 'Wyślij wiadomość'}
              </ButtonPrimary>
            </form>
          </div>
        </div>

        {/* Sekcja admina — lista wiadomości */}
        {isAdmin && token && (
          <div className="mt-12">
            <h2 className="text-2xl font-bold text-amber-400 mb-6">Wiadomości od użytkowników</h2>

            {mailsError && (
              <div className="mb-4 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
                {mailsError}
              </div>
            )}

            <div className="bg-gray-800/50 backdrop-blur-sm rounded-2xl border border-gray-700/50 overflow-hidden">
              {mailsLoading ? (
                <div className="p-8 text-center text-gray-400">Ładowanie...</div>
              ) : mails.length === 0 ? (
                <div className="p-8 text-center text-gray-400">Brak wiadomości</div>
              ) : (
                <div className="overflow-x-auto">
                  <table className="w-full">
                    <thead className="bg-gray-900/50">
                      <tr>
                        <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">Od</th>
                        <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">Temat</th>
                        <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">Data</th>
                        <th className="px-6 py-4 text-left text-xs font-semibold text-gray-400 uppercase tracking-wider">Akcje</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-gray-700/50">
                      {mails.map((mail) => (
                        <tr key={mail.id} className="hover:bg-gray-700/30 transition-colors">
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-white">{mail.email}</td>
                          <td className="px-6 py-4 text-sm text-gray-300 max-w-xs truncate">{mail.topic}</td>
                          <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-400">{formatDate(mail.createdAt)}</td>
                          <td className="px-6 py-4 whitespace-nowrap">
                            <button
                              onClick={() => handleMailClick(mail.id)}
                              disabled={loadingMailId === mail.id}
                              className="px-3 py-1.5 text-xs font-medium rounded-lg bg-cyan-500/20 text-cyan-400 border border-cyan-500/50 hover:bg-cyan-500/30 transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                              {loadingMailId === mail.id ? 'Ładowanie...' : 'Podgląd'}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}

              {mailsTotalPages > 1 && (
                <div className="px-6 py-4 border-t border-gray-700/50 flex items-center justify-between">
                  <div className="text-sm text-gray-400">
                    Łącznie: {mailsTotal} wiadomości
                  </div>
                  <div className="flex gap-2">
                    <button
                      onClick={() => setMailsPage(p => p - 1)}
                      disabled={mailsPage <= 1}
                      className="px-3 py-1.5 text-sm bg-gray-700/50 text-gray-300 rounded-lg hover:bg-gray-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Poprzednia
                    </button>
                    <span className="px-3 py-1.5 text-sm text-gray-400">
                      Strona {mailsPage} z {mailsTotalPages}
                    </span>
                    <button
                      onClick={() => setMailsPage(p => p + 1)}
                      disabled={mailsPage >= mailsTotalPages}
                      className="px-3 py-1.5 text-sm bg-gray-700/50 text-gray-300 rounded-lg hover:bg-gray-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                      Następna
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Modal podglądu wiadomości */}
        {modalOpen && selectedMail && (
          <div
            className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4"
            onClick={() => setModalOpen(false)}
          >
            <div
              className="bg-gray-800 rounded-2xl border border-gray-700 w-full max-w-lg p-6 shadow-2xl"
              onClick={e => e.stopPropagation()}
            >
              <div className="flex items-start justify-between mb-4">
                <h3 className="text-lg font-bold text-white">Wiadomość</h3>
                <button
                  onClick={() => setModalOpen(false)}
                  className="text-gray-400 hover:text-white transition-colors p-1"
                >
                  <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                  </svg>
                </button>
              </div>

              <div className="space-y-3 mb-6">
                <div>
                  <span className="text-xs text-gray-400 uppercase tracking-wider">Od</span>
                  <p className="text-white text-sm mt-1">{selectedMail.email}</p>
                </div>
                <div>
                  <span className="text-xs text-gray-400 uppercase tracking-wider">Temat</span>
                  <p className="text-white text-sm mt-1">{selectedMail.topic}</p>
                </div>
                <div>
                  <span className="text-xs text-gray-400 uppercase tracking-wider">Data</span>
                  <p className="text-white text-sm mt-1">{formatDate(selectedMail.createdAt)}</p>
                </div>
                <div>
                  <span className="text-xs text-gray-400 uppercase tracking-wider">Treść</span>
                  <p className="text-gray-300 text-sm mt-1 whitespace-pre-wrap break-words">{selectedMail.body}</p>
                </div>
              </div>

              <div className="flex justify-end">
                <button
                  onClick={() => handleDeleteMail(selectedMail.id)}
                  disabled={deletingId === selectedMail.id}
                  className="px-4 py-2 text-sm font-medium rounded-lg bg-red-500/20 text-red-400 border border-red-500/50 hover:bg-red-500/30 transition-all duration-200 disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  {deletingId === selectedMail.id ? 'Usuwanie...' : 'Usuń wiadomość'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </section>
  );
}

export default ContactPage;
