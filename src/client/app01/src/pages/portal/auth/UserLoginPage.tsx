import { useEffect, useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router';
import { AuthService } from '../../../services/api-auth-service';
import TextEdit from '../../../components/TextEdit';
import ButtonPrimary from '../../../components/ButtonPrimary';
import FormCard from '../../../components/FormCard';
import { setAuth } from '../../../utils/auth';

function UserLoginPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitStatus, setSubmitStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState('');
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const returnUrl = searchParams.get('returnUrl') || '/';

  useEffect(() => {
    document.title = 'Logowanie | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const [formData, setFormData] = useState({
    email: '',
    password: '',
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    setSubmitStatus('idle');
    setErrorMessage('');

    try {
      const authService = new AuthService(
        import.meta.env.VITE_API_URL,
        import.meta.env.VITE_APP_TOKEN
      );

      const response = await authService.userLogin({
        email: formData.email,
        password: formData.password,
      });

      // Zapisuje sesję i powiadamia inne komponenty o zmianie stanu logowania
      setAuth({
        token: response.token,
        tokenExpiresAt: response.tokenExpiresAt,
        email: response.email,
        id: response.id,
      });

      setSubmitStatus('success');

      setTimeout(() => {
        navigate(returnUrl);
      }, 1000);
    } catch (error) {
      setSubmitStatus('error');
      setErrorMessage(error instanceof Error ? error.message : 'Nieprawidłowy email lub hasło');
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
            Logowanie
          </h1>
          <p
            className={`text-gray-400 text-lg transition-all duration-700 ease-out delay-150 ${
              isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'
            }`}
          >
            Zaloguj się do swojego konta
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
              placeholder="Twoje hasło"
              required
            />

            {submitStatus === 'success' && (
              <div className="mb-4 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm">
                Logowanie pomyślne! Przekierowanie...
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
              {isSubmitting ? 'Logowanie...' : 'Zaloguj się'}
            </ButtonPrimary>

            <div className="mt-6 text-center">
              <p className="text-gray-400 text-sm">
                Nie masz jeszcze konta? Poproś o dostęp{' '}
                <Link to="/contact" className="text-cyan-400 hover:text-cyan-300 transition-colors">
                  Skontaktuj się...
                </Link>
              </p>
            </div>
          </form>
        </FormCard>
      </div>
    </section>
  );
}

export default UserLoginPage;
