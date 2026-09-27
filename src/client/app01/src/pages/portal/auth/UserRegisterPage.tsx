import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { AuthService } from '../../../services/api-auth-service';
import TextEdit from '../../../components/TextEdit';
import ButtonPrimary from '../../../components/ButtonPrimary';
import FormCard from '../../../components/FormCard';

function UserRegisterPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitStatus, setSubmitStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const navigate = useNavigate();

  useEffect(() => {
    document.title = 'Rejestracja | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const [formData, setFormData] = useState({
    email: '',
    password: '',
    confirmPassword: '',
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setSubmitStatus('idle');
    setErrorMessage('');

    if (formData.password !== formData.confirmPassword) {
      setSubmitStatus('error');
      setErrorMessage('Hasła nie są identyczne');
      setIsSubmitting(false);
      return;
    }

    if (formData.password.length < 6) {
      setSubmitStatus('error');
      setErrorMessage('Hasło musi mieć co najmniej 6 znaków');
      setIsSubmitting(false);
      return;
    }

    try {
      const authService = new AuthService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );

      await authService.userRegister({
        email: formData.email,
        password: formData.password,
      });

      setSubmitStatus('success');
      setFormData({ email: '', password: '', confirmPassword: '' });

      setTimeout(() => {
        navigate('/login');
      }, 2000);
    } catch (error) {
      setSubmitStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Wystąpił błąd podczas rejestracji');
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
            Rejestracja
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Utwórz nowe konto
          </p>
        </div>

        <FormCard isVisible={isVisible} borderColor="cyan">
          <form onSubmit={handleSubmit}>
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

            <TextEdit
              label="Hasło"
              id="password"
              name="password"
              type="password"
              value={formData.password}
              onChange={handleChange}
              placeholder="Minimum 6 znaków"
              required
              minLength={6}
            />

            <TextEdit
              label="Potwierdź hasło"
              id="confirmPassword"
              name="confirmPassword"
              type="password"
              value={formData.confirmPassword}
              onChange={handleChange}
              placeholder="Powtórz hasło"
              required
              minLength={6}
            />

            {submitStatus === 'success' && (
              <div className="mb-4 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
                Rejestracja zakończona pomyślnie! Przekierowanie do logowania...
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
              {isSubmitting ? 'Rejestracja...' : 'Zarejestruj'}
            </ButtonPrimary>

            <div className="mt-6 text-center">
              <p className="text-gray-400 text-sm">
                Masz już konto?{' '}
                <Link to="/login" className="text-cyan-400 hover:text-cyan-300 transition-colors">
                  Zaloguj się
                </Link>
              </p>
            </div>

            <div className="mt-4 text-center">
              <p className="text-gray-500 text-xs leading-relaxed">
                Rejestrując konto w serwisie tomsoft1.pl, wyrażasz zgodę na przechowywanie
                danych osobowych niezbędnych do jego utworzenia i obsługi.
              </p>
            </div>
          </form>
        </FormCard>
      </div>
    </section>
  );
}

export default UserRegisterPage;
