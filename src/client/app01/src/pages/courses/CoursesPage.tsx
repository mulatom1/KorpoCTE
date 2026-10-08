import { useEffect, useState } from "react";
import { ApiCoursesService } from "../../services/api-courses-service";
import type { CourseTileDto } from "../../services/contracts/courses-course-tiles-response";
import CourseTile from "../../components/CourseTile";
import SubMenu from "../../components/SubMenu";
import { isAuthenticated } from "../../utils/auth";
import { coursesSubMenuItems } from "./coursesSubMenu";

function CoursesPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [courses, setCourses] = useState<CourseTileDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState("");
  const apiUrl: string = import.meta.env.VITE_API_URL ?? "";

  useEffect(() => {
    document.title = "Kursy | tomsoft1 workspace";
    const timer = setTimeout(() => setIsVisible(true), 100);
    return () => clearTimeout(timer);
  }, []);

  useEffect(() => {
    let cancelled = false;

    const fetchCourses = async () => {
      try {
        // Lista kafelków jest publiczna – bez tokenu użytkownika.
        const apiService = new ApiCoursesService(
          apiUrl,
          import.meta.env.VITE_APP_TOKEN,
        );
        const response = await apiService.getCourseTiles();
        if (!cancelled) setCourses(response.courses);
      } catch (err) {
        if (!cancelled)
          setError(
            err instanceof Error ? err.message : "Błąd pobierania listy kursów",
          );
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    };

    fetchCourses();
    return () => {
      cancelled = true;
    };
  }, [apiUrl]);

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16">
      <div className="max-w-6xl mx-auto w-full">
        <div className="text-center mb-12">
          <h1
            className={`text-4xl sm:text-5xl font-bold mb-4 text-amber-400 transition-all duration-700 ease-out ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Kursy
          </h1>
          <p
            className={`text-gray-400 text-lg max-w-2xl mx-auto transition-all duration-700 ease-out delay-150 ${
              isVisible
                ? "opacity-100 translate-y-0"
                : "opacity-0 translate-y-8"
            }`}
          >
            Opublikowane kursy
          </p>
        </div>

        {/* Podmenu kursów tylko dla zalogowanych - gość widzi same kafelki */}
        {isAuthenticated() && (
          <SubMenu
            backPath="/"
            isVisible={isVisible}
            items={coursesSubMenuItems}
          />
        )}

        {error && (
          <div className="mb-6 p-4 bg-red-500/20 border border-red-500/50 rounded-xl text-red-400 text-sm">
            {error}
          </div>
        )}

        {isLoading ? (
          <div className="p-8 text-center text-gray-400">Ładowanie...</div>
        ) : error ? null : courses.length === 0 ? (
          <div className="p-8 text-center text-gray-400">
            Brak opublikowanych kursów
          </div>
        ) : (
          <div className="flex flex-wrap justify-center gap-6">
            {courses.map((course, index) => (
              <CourseTile
                key={course.slug}
                tile={course}
                apiUrl={apiUrl}
                index={index}
                isVisible={isVisible}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}

export default CoursesPage;
