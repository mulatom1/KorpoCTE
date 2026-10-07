import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ApiCoursesService } from "../../services/api-courses-service";
import type { CoursesCourseContentResponse } from "../../services/contracts/courses-course-content-response";
import ButtonSecondary from "../../components/ButtonSecondary";
import CourseMarkdown from "../../components/CourseMarkdown";

function CourseDetailsPage() {
  const { slug } = useParams<{ slug: string }>();
  const navigate = useNavigate();
  const [course, setCourse] = useState<CoursesCourseContentResponse | null>(
    null,
  );
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");
  const apiUrl: string = import.meta.env.VITE_API_URL ?? "";

  useEffect(() => {
    document.title = course
      ? `${course.title} | tomsoft1 workspace`
      : "Kurs | tomsoft1 workspace";
  }, [course]);

  useEffect(() => {
    let cancelled = false;

    const fetchCourse = async () => {
      setIsLoading(true);
      setError("");
      setCourse(null);

      try {
        // Dostęp do strony pilnuje RequireAuth (routing) – tutaj token jest już ważny.
        const apiService = new ApiCoursesService(
          apiUrl,
          import.meta.env.VITE_APP_TOKEN,
        );
        apiService.setUsrToken(localStorage.getItem("token") ?? "");

        const response = await apiService.getCourseContent({
          slug: slug ?? "",
        });
        if (!cancelled) setCourse(response);
      } catch (err) {
        if (!cancelled)
          setError(
            err instanceof Error ? err.message : "Błąd pobierania treści kursu",
          );
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    fetchCourse();
    return () => {
      cancelled = true;
    };
  }, [apiUrl, slug]);

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16">
      <div className="max-w-4xl mx-auto w-full min-w-0">
        <div className="mb-8">
          <ButtonSecondary type="button" onClick={() => navigate("/courses")}>
            Powrót do kursów
          </ButtonSecondary>
        </div>

        {error && (
          <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
            {error}
          </div>
        )}

        {isLoading ? (
          <div className="p-8 text-center text-gray-400">Ładowanie...</div>
        ) : course ? (
          <article className="min-w-0">
            <h1 className="text-4xl sm:text-5xl font-bold mb-4 text-amber-400 break-words">
              {course.title}
            </h1>
            {course.tags.length > 0 && (
              <ul className="flex flex-wrap gap-2 mb-8">
                {course.tags.map((tag) => (
                  <li
                    key={tag}
                    className="px-2 py-1 text-xs font-semibold rounded-full bg-cyan-500/20 text-cyan-400 border border-cyan-500/50"
                  >
                    {tag}
                  </li>
                ))}
              </ul>
            )}
            <CourseMarkdown
              content={course.content}
              apiUrl={apiUrl}
              mediaBaseUrl={course.mediaBaseUrl}
            />
          </article>
        ) : null}
      </div>
    </section>
  );
}

export default CourseDetailsPage;
