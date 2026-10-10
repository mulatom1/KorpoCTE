import { createRoot } from "react-dom/client";
import { BrowserRouter, Navigate, Route, Routes } from "react-router";
import "./index.css";

import Layout from "./components/Layout";
import RequireAuth from "./components/RequireAuth";

// Portal module pages
import HomePage from "./pages/portal/home/HomePage";
import AboutMePage from "./pages/portal/about/AboutMePage";
import ContactPage from "./pages/portal/contact/ContactPage";
import MyCvPage from "./pages/portal/my-cv/MyCvPage";
import UserRegisterPage from "./pages/portal/auth/UserRegisterPage";
import UserLoginPage from "./pages/portal/auth/UserLoginPage";
import UserPassChangePage from "./pages/portal/auth/UserPassChangePage";
import UserPage from "./pages/portal/user/UserPage";

// Apps module pages
import AppsPage from "./pages/apps/AppsPage";

// Games module pages
import GamesPage from "./pages/games/GamesPage";

// Courses module pages
import CoursesPage from "./pages/courses/CoursesPage";
import CourseDetailsPage from "./pages/courses/CourseDetailsPage";
import TomoAiTerminalPage from "./pages/courses/TomoAiTerminalPage";
import HangarPage from "./pages/courses/HangarPage";
import LeaderboardPage from "./pages/courses/LeaderboardPage";

// Flashcards module pages
import FlashcardsPage from "./pages/flashcards/FlashcardsPage";

// Lotto module pages
import LottoPage from "./pages/lotto/LottoPage";
import LottoDrawsPage from "./pages/lotto/LottoDrawsPage";
import LottoDrawsNumbersStatsPage from "./pages/lotto/LottoDrawsNumbersStatsPage";
import LottoTicketsPage from "./pages/lotto/LottoTicketsPage";
import LottoWinningTicketsPage from "./pages/lotto/LottoWinningTicketsPage";

createRoot(document.getElementById("root")!).render(
  <BrowserRouter basename={import.meta.env.BASE_URL}>
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<HomePage />} />
        <Route path="apps" element={<AppsPage />} />
        <Route path="games" element={<GamesPage />} />
        <Route path="courses" element={<CoursesPage />} />
        <Route path="about" element={<AboutMePage />} />
        <Route path="my-cv" element={<MyCvPage />} />
        <Route path="contact" element={<ContactPage />} />
        <Route path="register" element={<UserRegisterPage />} />
        <Route path="login" element={<UserLoginPage />} />
        <Route path="pass-change" element={<UserPassChangePage />} />
        <Route path="flashcards" element={<FlashcardsPage />} />
        <Route path="lotto" element={<LottoPage />} />

        {/* Podstrony wymagające zalogowania – brak/wygasły token => /login?returnUrl=... */}
        <Route element={<RequireAuth />}>
          <Route path="courses/:slug" element={<CourseDetailsPage />} />
          <Route path="tomo-ai-001" element={<TomoAiTerminalPage />} />
          <Route path="hangar" element={<HangarPage />} />
          <Route path="leaderboard" element={<LeaderboardPage />} />
          <Route path="users" element={<UserPage />} />
          <Route path="lotto/draws" element={<LottoDrawsPage />} />
          <Route
            path="lotto/draws-numbers-stats"
            element={<LottoDrawsNumbersStatsPage />}
          />
          <Route path="lotto/tickets" element={<LottoTicketsPage />} />
          <Route
            path="lotto/winning-tickets"
            element={<LottoWinningTicketsPage />}
          />
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  </BrowserRouter>,
);
