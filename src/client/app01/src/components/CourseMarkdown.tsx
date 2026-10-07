import ReactMarkdown, { defaultUrlTransform } from "react-markdown";
import type { Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeHighlight from "rehype-highlight";
import "highlight.js/styles/github-dark.css";

import { resolveCourseMediaUrl } from "../utils/courseMediaUrl";

interface CourseMarkdownProps {
  content: string;
  apiUrl: string;
  mediaBaseUrl: string;
}

const EXTERNAL_LINK = /^https?:\/\//i;

// Mapowanie elementów Markdown na klasy Tailwind (czytelna typografia, mobile).
const components: Components = {
  h1: ({ children }) => (
    <h1 className="text-3xl font-bold text-amber-400 mt-8 mb-4">{children}</h1>
  ),
  h2: ({ children }) => (
    <h2 className="text-2xl font-bold text-cyan-400 mt-8 mb-3">{children}</h2>
  ),
  h3: ({ children }) => (
    <h3 className="text-xl font-semibold text-white mt-6 mb-2">{children}</h3>
  ),
  p: ({ children }) => (
    <p className="text-gray-300 leading-relaxed mb-4">{children}</p>
  ),
  a: ({ href, children }) => {
    const isExternal = !!href && EXTERNAL_LINK.test(href);
    return (
      <a
        href={href}
        className="text-cyan-400 underline hover:text-cyan-300"
        {...(isExternal
          ? { target: "_blank", rel: "noopener noreferrer" }
          : undefined)}
      >
        {children}
      </a>
    );
  },
  ul: ({ children }) => (
    <ul className="list-disc pl-6 mb-4 text-gray-300 space-y-1">{children}</ul>
  ),
  ol: ({ children, start }) => (
    <ol
      start={start}
      className="list-decimal pl-6 mb-4 text-gray-300 space-y-1"
    >
      {children}
    </ol>
  ),
  li: ({ children }) => <li className="leading-relaxed">{children}</li>,
  blockquote: ({ children }) => (
    <blockquote className="border-l-4 border-cyan-500/50 pl-4 italic text-gray-400 mb-4">
      {children}
    </blockquote>
  ),
  // Kod w bloku (pre > code) dostaje tło z motywu highlight.js; kod w linii – własne.
  pre: ({ children }) => (
    <pre className="overflow-x-auto rounded-xl mb-4 text-sm bg-[#0d1117] [&>code]:block [&>code]:p-4 [&>code]:bg-transparent [&>code]:text-gray-200">
      {children}
    </pre>
  ),
  code: ({ className, children }) => (
    <code
      className={`${className ?? ""} rounded bg-gray-900/70 px-1 py-0.5 font-mono text-amber-300`}
    >
      {children}
    </code>
  ),
  table: ({ children }) => (
    <div className="overflow-x-auto mb-4">
      <table className="min-w-full border-collapse text-sm text-gray-300">
        {children}
      </table>
    </div>
  ),
  th: ({ children, style }) => (
    <th
      style={style}
      className="border border-gray-700 bg-gray-800 px-3 py-2 text-left font-semibold text-white"
    >
      {children}
    </th>
  ),
  td: ({ children, style }) => (
    <td style={style} className="border border-gray-700 px-3 py-2">
      {children}
    </td>
  ),
  img: ({ src, alt, title }) => (
    <img
      src={src}
      alt={alt ?? ""}
      title={title}
      loading="lazy"
      className="max-w-full h-auto rounded-xl my-4"
    />
  ),
};

// Render treści kursu – bez surowego HTML (brak rehype-raw), URL-e mediów
// względne wobec katalogu kursu, każdy URL sanityzowany przez defaultUrlTransform.
function CourseMarkdown({
  content,
  apiUrl,
  mediaBaseUrl,
}: CourseMarkdownProps) {
  return (
    <div className="course-markdown break-words">
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[rehypeHighlight]}
        urlTransform={(url) =>
          defaultUrlTransform(resolveCourseMediaUrl(url, apiUrl, mediaBaseUrl))
        }
        components={components}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}

export default CourseMarkdown;
