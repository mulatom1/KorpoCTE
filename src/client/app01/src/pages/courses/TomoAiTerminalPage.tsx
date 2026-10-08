import { useCallback, useEffect, useState } from "react";
import { ApiCoursesService } from "../../services/api-courses-service";
import type { CoursesHangarTaskDto } from "../../services/contracts/courses-hangar-tasks-response";
import type { CoursesVerifyAnswerRequest } from "../../services/contracts/courses-verify-answer-request";
import type { CoursesVerifyAnswerResponse } from "../../services/contracts/courses-verify-answer-response";
import FormCard from "../../components/FormCard";
import TextEdit from "../../components/TextEdit";
import ButtonPrimary from "../../components/ButtonPrimary";
import ButtonSecondary from "../../components/ButtonSecondary";
import SubMenu from "../../components/SubMenu";
import { coursesSubMenuItems } from "./coursesSubMenu";

const ANSWER_MAX_LENGTH = 4000;

// Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
function createApiService(): ApiCoursesService {
  const apiService = new ApiCoursesService(
    import.meta.env.VITE_API_URL ?? "",
    import.meta.env.VITE_APP_TOKEN,
  );
  apiService.setUsrToken(localStorage.getItem("token") ?? "");
  return apiService;
}

function TomoAiTerminalPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [tasks, setTasks] = useState<CoursesHangarTaskDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedFlagId, setSelectedFlagId] = useState("");
  const [answer, setAnswer] = useState("");
  const [isChecking, setIsChecking] = useState(false);
  const [result, setResult] = useState<CoursesVerifyAnswerResponse | null>(
    null,
  );
  // Ostatnio wysłana para flagId + answer – do ponowienia po awarii.
  const [lastRequest, setLastRequest] =
    useState<CoursesVerifyAnswerRequest | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    document.title = "Terminal TOMO-AI-001 | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  const fetchTasks = useCallback(async () => {
    setIsLoading(true);
    setError("");
    try {
      const response = await createApiService().getHangarTasks();
      setTasks(response.tasks);
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Błąd pobierania listy zadań",
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTasks();
  }, [fetchTasks]);

  const canSubmit =
    selectedFlagId !== "" && answer.trim().length > 0 && !isChecking;

  const verify = async (request: CoursesVerifyAnswerRequest) => {
    setIsChecking(true);
    setResult(null);
    setError("");
    setLastRequest(request);

    try {
      const response = await createApiService().verifyAnswer(request);
      setResult(response);

      if (response.status === "Correct") {
        // Flaga nie jest jeszcze zdobyta (aktywacja kodem) – lista bez zmian.
        setAnswer("");
      } else if (response.status === "AlreadyOwned") {
        setSelectedFlagId("");
        await fetchTasks();
      }
    } catch (err) {
      setError(
        err instanceof Error ? err.message : "Błąd weryfikacji odpowiedzi",
      );
    } finally {
      setIsChecking(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;
    verify({ flagId: Number(selectedFlagId), answer });
  };

  const handleRetry = () => {
    if (lastRequest) verify(lastRequest);
  };

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16">
      <div className="max-w-3xl mx-auto w-full min-w-0">
        <div className="text-center mb-12">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Terminal TOMO-AI-001
          </h1>
          <p className="text-gray-400 text-lg max-w-2xl mx-auto">
            Wybierz zadanie z kursu, wpisz odpowiedź i oddaj ją do sprawdzenia.
          </p>
        </div>

        <SubMenu
          backPath="/courses"
          isVisible={isVisible}
          items={coursesSubMenuItems}
        />

        <FormCard isVisible={isVisible} borderColor="cyan">
          <h2 className="text-2xl font-bold text-cyan-400 mb-6">
            Do sprawdzenia
          </h2>

          {isLoading ? (
            <div className="p-8 text-center text-gray-400">Ładowanie...</div>
          ) : error && tasks.length === 0 ? null : tasks.length === 0 ? (
            <div className="p-8 text-center text-gray-400">
              Brak zadań do sprawdzenia
            </div>
          ) : (
            <form onSubmit={handleSubmit}>
              <div className="mb-4">
                <label
                  htmlFor="flagId"
                  className="block text-gray-300 text-sm font-medium mb-2"
                >
                  Zadanie
                </label>
                <select
                  id="flagId"
                  name="flagId"
                  value={selectedFlagId}
                  onChange={(e) => setSelectedFlagId(e.target.value)}
                  className="w-full px-4 py-2 bg-gray-900/50 border border-gray-700 rounded-xl text-white focus:outline-none focus:border-cyan-500/50 focus:ring-1 focus:ring-cyan-500/50 transition-colors"
                >
                  <option value="">Wybierz zadanie...</option>
                  {tasks.map((task) => (
                    <option
                      key={task.flagId}
                      value={String(task.flagId)}
                      disabled={task.isOwned}
                    >
                      {task.isOwned ? `${task.title} (zdobyta)` : task.title}
                    </option>
                  ))}
                </select>
              </div>

              <TextEdit
                label="Twoja odpowiedź"
                id="answer"
                name="answer"
                type="textarea"
                rows={8}
                maxLength={ANSWER_MAX_LENGTH}
                value={answer}
                onChange={(e) => setAnswer(e.target.value)}
                placeholder="Wpisz odpowiedź..."
                className="mb-1"
              />
              <p className="text-right text-xs text-gray-500 mb-4">
                {answer.length}/{ANSWER_MAX_LENGTH}
              </p>

              <ButtonPrimary
                className="w-full py-3"
                type="submit"
                disabled={!canSubmit}
              >
                {isChecking ? "Sprawdzam…" : "Sprawdź"}
              </ButtonPrimary>
            </form>
          )}

          {error && (
            <div
              role="alert"
              className="mt-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm"
            >
              {error}
            </div>
          )}

          {result?.status === "Correct" && (
            <div
              role="status"
              className="mt-6 p-4 bg-green-500/20 border border-green-500/50 rounded-xl text-green-400 text-sm space-y-3"
            >
              {/* Treść komunikatu pochodzi wyłącznie z serwera - bez powtórzeń w kliencie */}
              <p className="font-semibold">{result.message}</p>
              {result.code && (
                <>
                  <p className="text-gray-300">Twój kod flagi:</p>
                  <code
                    data-testid="flag-code"
                    className="block select-all break-all px-4 py-3 bg-gray-900/70 border border-green-500/50 rounded-xl text-lg font-mono text-green-300"
                  >
                    {result.code}
                  </code>
                </>
              )}
            </div>
          )}

          {result?.status === "Incorrect" && (
            <div
              role="status"
              className="mt-6 p-4 bg-orange-500/20 border border-orange-500/50 rounded-xl text-orange-300 text-sm"
            >
              <p className="font-semibold">{result.message}</p>
            </div>
          )}

          {result?.status === "Unavailable" && (
            <div
              role="status"
              className="mt-6 p-4 bg-gray-700/40 border border-yellow-500/50 rounded-xl text-yellow-300 text-sm space-y-3"
            >
              <p className="font-semibold">{result.message}</p>
              <ButtonSecondary
                type="button"
                onClick={handleRetry}
                disabled={isChecking}
              >
                Spróbuj ponownie
              </ButtonSecondary>
            </div>
          )}

          {result?.status === "AlreadyOwned" && (
            <div
              role="status"
              className="mt-6 p-4 bg-cyan-500/20 border border-cyan-500/50 rounded-xl text-cyan-300 text-sm"
            >
              <p>{result.message}</p>
            </div>
          )}
        </FormCard>
      </div>
    </section>
  );
}

export default TomoAiTerminalPage;
