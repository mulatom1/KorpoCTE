export interface FlashcardsGenerateFromTextResponse {
  flashcards: { question: string; answer: string }[];
  generatedCount: number;
  inputTextLength: number;
}
