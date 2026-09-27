export interface Flashcard {
  id: string;
  question: string;
  answer: string;
  groupName?: string;
  createdAt: string;
  updatedAt: string;
  source?: "manual" | "ai";
}

export interface FlashcardGroup {
  name: string;
  count: number;
}

interface FlashcardsStorage {
  flashcards: Flashcard[];
  lastUpdated: string;
}

const STORAGE_KEY = "flashcards";
const MAX_FLASHCARDS = 500;
const WARNING_THRESHOLD = 400;

function generateId(): string {
  return crypto.randomUUID();
}

function loadStorage(): FlashcardsStorage {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return { flashcards: [], lastUpdated: new Date().toISOString() };
  }
  return JSON.parse(raw);
}

function saveStorage(storage: FlashcardsStorage): void {
  storage.lastUpdated = new Date().toISOString();
  localStorage.setItem(STORAGE_KEY, JSON.stringify(storage));
}

export function getAll(): Flashcard[] {
  return loadStorage().flashcards;
}

export function getByGroup(groupName: string): Flashcard[] {
  return loadStorage().flashcards.filter((f) => f.groupName === groupName);
}

export function getGroups(): FlashcardGroup[] {
  const flashcards = loadStorage().flashcards;
  const groupMap = new Map<string, number>();
  for (const f of flashcards) {
    const name = f.groupName || "";
    if (name) {
      groupMap.set(name, (groupMap.get(name) || 0) + 1);
    }
  }
  return Array.from(groupMap.entries()).map(([name, count]) => ({
    name,
    count,
  }));
}

export function add(data: {
  question: string;
  answer: string;
  groupName?: string;
  source?: "manual" | "ai";
}): Flashcard {
  const storage = loadStorage();
  if (storage.flashcards.length >= MAX_FLASHCARDS) {
    throw new Error(`Osiągnięto limit ${MAX_FLASHCARDS} fiszek.`);
  }
  const now = new Date().toISOString();
  const flashcard: Flashcard = {
    id: generateId(),
    question: data.question,
    answer: data.answer,
    groupName: data.groupName,
    createdAt: now,
    updatedAt: now,
    source: data.source || "manual",
  };
  storage.flashcards.push(flashcard);
  saveStorage(storage);
  return flashcard;
}

export function update(
  id: string,
  data: Partial<Pick<Flashcard, "question" | "answer" | "groupName">>,
): Flashcard {
  const storage = loadStorage();
  const index = storage.flashcards.findIndex((f) => f.id === id);
  if (index === -1) {
    throw new Error("Fiszka nie znaleziona.");
  }
  const flashcard = storage.flashcards[index];
  if (data.question !== undefined) flashcard.question = data.question;
  if (data.answer !== undefined) flashcard.answer = data.answer;
  if (data.groupName !== undefined) flashcard.groupName = data.groupName;
  flashcard.updatedAt = new Date().toISOString();
  storage.flashcards[index] = flashcard;
  saveStorage(storage);
  return flashcard;
}

export function remove(id: string): void {
  const storage = loadStorage();
  storage.flashcards = storage.flashcards.filter((f) => f.id !== id);
  saveStorage(storage);
}

export function addBatch(
  items: {
    question: string;
    answer: string;
    groupName?: string;
    source?: "manual" | "ai";
  }[],
): Flashcard[] {
  const storage = loadStorage();
  const remaining = MAX_FLASHCARDS - storage.flashcards.length;
  if (items.length > remaining) {
    throw new Error(
      `Można dodać maksymalnie ${remaining} fiszek (limit: ${MAX_FLASHCARDS}).`,
    );
  }
  const now = new Date().toISOString();
  const newFlashcards: Flashcard[] = items.map((item) => ({
    id: generateId(),
    question: item.question,
    answer: item.answer,
    groupName: item.groupName,
    createdAt: now,
    updatedAt: now,
    source: item.source || "manual",
  }));
  storage.flashcards.push(...newFlashcards);
  saveStorage(storage);
  return newFlashcards;
}

export function getCount(): number {
  return loadStorage().flashcards.length;
}

export function isNearLimit(): boolean {
  return getCount() >= WARNING_THRESHOLD;
}

export function clear(): void {
  const storage: FlashcardsStorage = {
    flashcards: [],
    lastUpdated: new Date().toISOString(),
  };
  localStorage.setItem(STORAGE_KEY, JSON.stringify(storage));
}
