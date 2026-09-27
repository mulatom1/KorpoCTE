export interface CourseFrontmatter {
  subtitle?: string;
}

/**
 * Parsuje YAML frontmatter z pliku Markdown.
 * Obsługuje bloki ---...--- na początku pliku.
 * Nie wymaga zewnętrznych zależności – działa w przeglądarce.
 */
export function parseFrontmatter(raw: string): {
  frontmatter: CourseFrontmatter;
  content: string;
} {
  const match = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n?([\s\S]*)$/);
  if (!match) {
    return { frontmatter: {}, content: raw };
  }

  const yamlStr = match[1];
  const content = match[2];
  const frontmatter: CourseFrontmatter = {};

  for (const line of yamlStr.split(/\r?\n/)) {
    const kv = line.match(/^(\w+):\s*["']?(.*?)["']?\s*$/);
    if (!kv) continue;
    const [, key, value] = kv;
    if (key === "subtitle") frontmatter.subtitle = value;
  }

  return { frontmatter, content };
}
