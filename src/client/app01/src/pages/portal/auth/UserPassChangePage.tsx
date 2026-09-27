import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { AuthService } from '../../../services/api-auth-service';
import TextEdit from '../../../components/TextEdit';
import ButtonPrimary from '../../../components/ButtonPrimary';
import FormCard from '../../../components/FormCard';

function UserPassChangePage() {
  const [isVisible, setIsVisible] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitStatus, setSubmitStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    document.title = 'Zmiana hasła | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const [formData, setFormData] = useState({
    login: '',
    password1: '',
    password2: '',
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setSubmitStatus('idle');
    setErrorMessage('');

    if (formData.password2.length < 6) {
      setSubmitStatus('error');
      setErrorMessage('Nowe hasło musi mieć co najmniej 6 znaków');
      setIsSubmitting(false);
      return;
    }

    try {
      const authService = new AuthService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );

      await authService.userPassChange({
        login: formData.login,
        password1: formData.password1,
        password2: formData.password2,
      });

      setSubmitStatus('success');
      setFormData({ login: '', password1: '', password2: '' });

      setTimeout(() => {
        navigate('/login');
      }, 2000);
    } catch (error) {
      setSubmitStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Wystąpił błąd podczas zmiany hasła');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    setFormData({
      ...formData,
      [e.target.name]: e.target.value,
    });
  };

  return (
    <section className="min-h-[calc(100vh-4rem)] flex items-center justify-center px-4 py-16 overflow-hidden">
      <div className="max-w-md mx-auto w-full">
        <div className="text-center mb-8">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Zmiana hasła
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Zaktualizuj swoje hasło
          </p>
        </div>

        <FormCard isVisible={isVisible} borderColor="cyan">
          <form onSubmit={handleSubmit}>
            <TextEdit
              label="Login (Email)"
              id="login"
              name="login"
              type="email"
              value={formData.login}
              onChange={handleChange}
              placeholder="jan@example.com"
              required
            />

            <TextEdit
              label="Aktualne hasło"
              id="password1"
              name="password1"
              type="password"
              value={formData.password1}
              onChange={handleChange}
              placeholder="Podaj aktualne hasło"
              required
            />

            <TextEdit
              label="Nowe hasło"
              id="password2"
              name="password2"
              type="password"
              value={formData.password2}
              onChange={handleChange}
              placeholder="Minimum 6 znaków"
              required
              minLength={6}
            />

            {submitStatus === 'success' && (
              <div className="mb-4 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
                Hasło zostało zmienione pomyślnie! Przekierowanie do logowania...
              </div>
            )}

            {submitStatus === 'error' && (
              <div className="mb-4 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
                {errorMessage}
              </div>
            )}

            <ButtonPrimary
              type="submit"
              disabled={isSubmitting}
              className="w-full py-3"
            >
              {isSubmitting ? 'Zmiana hasła...' : 'Zmień hasło'}
            </ButtonPrimary>

            <div className="mt-6 text-center">
              <p className="text-gray-400 text-sm">
                Pamiętasz hasło?{' '}
                <Link to="/login" className="text-cyan-400 hover:text-cyan-300 transition-colors">
                  Zaloguj się
                </Link>
              </p>
            </div>
          </form>
        </FormCard>
      </div>
    </section>
  );
}

export default UserPassChangePage;
