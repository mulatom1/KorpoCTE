import { useEffect, useState } from 'react';
import { ApiFlashcardsService } from '../../services/api-flashcards-service';
import ConfirmModal from '../../components/ConfirmModal';
import TextEdit from '../../components/TextEdit';
import ButtonPrimary from '../../components/ButtonPrimary';
import ButtonSecondary from '../../components/ButtonSecondary';
import ButtonDanger from '../../components/ButtonDanger';
import FormCard from '../../components/FormCard';
import * as flashcardsService from '../../services/flashcards-service';
import type { Flashcard } from '../../services/flashcards-service';

// ─── FlashcardItem ──────────────────────────────────────────────

interface FlashcardItemProps {
  flashcard: Flashcard;
  onEdit: (flashcard: Flashcard) => void;
  onDelete: (id: string) => void;
}

function FlashcardItem({ flashcard, onEdit, onDelete }: FlashcardItemProps) {
  const [flipped, setFlipped] = useState(false);

  return (
    <div
      className="flashcard-scene cursor-pointer group"
      onClick={() => setFlipped(!flipped)}
    >
      <div className={`flashcard-card ${flipped ? 'flipped' : ''}`}>
        {/* Front - pytanie */}
        <div className="flashcard-front p-5 flex flex-col justify-between"
          style={{
            background: 'linear-gradient(135deg, #fef9c3, #fde68a)',
            border: '2px solid #f59e0b',
            boxShadow: '0 8px 30px rgba(0,0,0,0.5)',
          }}
        >
          <div className="flex items-start justify-between mb-2">
            <span className="text-xs font-bold px-2 py-0.5 rounded" style={{ color: '#92400e', backgroundColor: 'rgba(146,64,14,0.15)' }}>
              {flashcard.groupName || 'Bez grupy'}
            </span>
            <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity" onClick={(e) => e.stopPropagation()}>
              <button
                onClick={() => onEdit(flashcard)}
                className="p-1.5 rounded-lg transition-colors"
                style={{ backgroundColor: 'rgba(255,255,255,0.7)', color: '#92400e' }}
                title="Edytuj"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                </svg>
              </button>
              <button
                onClick={() => onDelete(flashcard.id)}
                className="p-1.5 rounded-lg transition-colors"
                style={{ backgroundColor: 'rgba(255,255,255,0.7)', color: '#dc2626' }}
                title="Usuń"
              >
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </button>
            </div>
          </div>
          <div className="flex-1 flex items-center justify-center">
            <p className="text-center text-lg leading-relaxed font-bold" style={{ color: '#1c1917' }}>{flashcard.question}</p>
          </div>
          <div className="text-xs font-medium text-center mt-2" style={{ color: '#92400e' }}>
            Kliknij aby zobaczyć odpowiedź
          </div>
        </div>

        {/* Back - odpowiedź */}
        <div className="flashcard-back p-5 flex flex-col justify-between"
          style={{
            background: 'linear-gradient(135deg, #dbeafe, #93c5fd)',
            border: '2px solid #3b82f6',
            boxShadow: '0 8px 30px rgba(0,0,0,0.5)',
          }}
        >
          <div className="flex items-start justify-between mb-2">
            <span className="text-xs font-bold px-2 py-0.5 rounded" style={{ color: '#1e40af', backgroundColor: 'rgba(30,64,175,0.15)' }}>
              Odpowiedź
            </span>
            {flashcard.source === 'ai' && (
              <span className="text-xs font-bold px-2 py-0.5 rounded" style={{ color: '#7c3aed', backgroundColor: 'rgba(124,58,237,0.15)' }}>
                AI
              </span>
            )}
          </div>
          <div className="flex-1 flex items-center justify-center">
            <p className="text-center text-lg leading-relaxed font-semibold" style={{ color: '#1c1917' }}>{flashcard.answer}</p>
          </div>
          <div className="text-xs font-medium text-center mt-2" style={{ color: '#1e40af' }}>
            Kliknij aby wrócić do pytania
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── FlashcardModal ─────────────────────────────────────────────

interface FlashcardModalProps {
  isOpen: boolean;
  flashcard: Flashcard | null;
  groups: string[];
  onSave: (data: { question: string; answer: string; groupName?: string }) => void;
  onCancel: () => void;
}

function FlashcardModal({ isOpen, flashcard, groups, onSave, onCancel }: FlashcardModalProps) {
  const [question, setQuestion] = useState('');
  const [answer, setAnswer] = useState('');
  const [groupName, setGroupName] = useState('');
  const [showGroupSuggestions, setShowGroupSuggestions] = useState(false);
  const [errors, setErrors] = useState<{ question?: string; answer?: string }>({});

  useEffect(() => {
    if (isOpen) {
      setQuestion(flashcard?.question || '');
      setAnswer(flashcard?.answer || '');
      setGroupName(flashcard?.groupName || '');
      setErrors({});
    }
  }, [isOpen, flashcard]);

  const filteredGroups = groups.filter((g) =>
    g.toLowerCase().includes(groupName.toLowerCase()) && g.toLowerCase() !== groupName.toLowerCase()
  );

  const validate = (): boolean => {
    const newErrors: { question?: string; answer?: string } = {};
    if (!question.trim() || question.trim().length < 2) newErrors.question = 'Pytanie musi mieć min. 2 znaki';
    if (!answer.trim() || answer.trim().length < 2) newErrors.answer = 'Odpowiedź musi mieć min. 2 znaki';
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSubmit = () => {
    if (!validate()) return;
    onSave({ question: question.trim(), answer: answer.trim(), groupName: groupName.trim() || undefined });
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm" onClick={onCancel}>
      <div
        className="mx-4 max-w-lg w-full"
        onClick={(e) => e.stopPropagation()}
      >
        <FormCard isVisible={true} borderColor="cyan" className="shadow-2xl">
          <h3 className="text-xl font-bold text-white mb-4">
            {flashcard ? 'Edytuj fiszkę' : 'Dodaj fiszkę'}
          </h3>

          <div className="space-y-4">
            <TextEdit
              label="Pytanie *"
              id="question"
              name="question"
              type="text"
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="Wpisz pytanie..."
              required
              error={errors.question}
            />

            <TextEdit
              label="Odpowiedź *"
              id="answer"
              name="answer"
              type="text"
              value={answer}
              onChange={(e) => setAnswer(e.target.value)}
              placeholder="Wpisz odpowiedź..."
              required
              error={errors.answer}
            />

            <div className="relative">
              <TextEdit
                label="Grupa (opcjonalnie)"
                id="groupName"
                name="groupName"
                type="text"
                value={groupName}
                onChange={(e) => { setGroupName(e.target.value); setShowGroupSuggestions(true); }}
                placeholder="Np. Biologia, Historia..."
                error={undefined}
              />
              {showGroupSuggestions && filteredGroups.length > 0 && (
                <div className="absolute z-10 w-full mt-1 bg-gray-700 border border-gray-600 rounded-xl shadow-lg max-h-32 overflow-y-auto">
                  {filteredGroups.map((g) => (
                    <button
                      key={g}
                      type="button"
                      onMouseDown={() => { setGroupName(g); setShowGroupSuggestions(false); }}
                      className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-600 hover:text-white transition-colors first:rounded-t-xl last:rounded-b-xl"
                    >
                      {g}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </div>

          <div className="flex gap-3 mt-6">
            <ButtonSecondary onClick={onCancel} className="flex-1">
              Anuluj
            </ButtonSecondary>
            <ButtonPrimary onClick={handleSubmit} className="flex-1">
              {flashcard ? 'Aktualizuj' : 'Zapisz'}
            </ButtonPrimary>
          </div>
        </FormCard>
      </div>
    </div>
  );
}

// ─── GenerateModal ──────────────────────────────────────────────

interface GenerateModalProps {
  isOpen: boolean;
  groups: string[];
  onSave: (items: { question: string; answer: string; groupName?: string }[]) => void;
  onCancel: () => void;
}

function GenerateModal({ isOpen, groups, onSave, onCancel }: GenerateModalProps) {
  const [text, setText] = useState('');
  const [count, setCount] = useState(5);
  const [groupName, setGroupName] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [generated, setGenerated] = useState<{ question: string; answer: string }[]>([]);
  const [showPreview, setShowPreview] = useState(false);
  const [showGroupSuggestions, setShowGroupSuggestions] = useState(false);

  useEffect(() => {
    if (isOpen) {
      setText('');
      setCount(5);
      setGroupName('');
      setError('');
      setGenerated([]);
      setShowPreview(false);
    }
  }, [isOpen]);

  const filteredGroups = groups.filter((g) =>
    g.toLowerCase().includes(groupName.toLowerCase()) && g.toLowerCase() !== groupName.toLowerCase()
  );

  const handleGenerate = async () => {
    if (text.trim().length < 200) {
      setError('Tekst musi mieć minimum 200 znaków.');
      return;
    }
    if (text.trim().length > 10000) {
      setError('Tekst nie może przekraczać 10 000 znaków.');
      return;
    }

    setLoading(true);
    setError('');
    try {
      const apiFlashcardsService = new ApiFlashcardsService(import.meta.env.VITE_API_URL, import.meta.env.VITE_APP_TOKEN);
      const token = localStorage.getItem('token') || '';
      apiFlashcardsService.setUsrToken(token);

      const response = await apiFlashcardsService.flashcardsGenerateFromText({
        text: text.trim(),
        count,
        groupName: groupName.trim() || undefined,
      });

      setGenerated(response.flashcards);
      setShowPreview(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Błąd generowania fiszek.');
    } finally {
      setLoading(false);
    }
  };

  const handleEditGenerated = (index: number, field: 'question' | 'answer', value: string) => {
    setGenerated((prev) => prev.map((item, i) => i === index ? { ...item, [field]: value } : item));
  };

  const handleRemoveGenerated = (index: number) => {
    setGenerated((prev) => prev.filter((_, i) => i !== index));
  };

  const handleSaveAll = () => {
    if (generated.length === 0) return;
    onSave(generated.map((item) => ({ ...item, groupName: groupName.trim() || undefined })));
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/80 backdrop-blur-sm p-4" onClick={onCancel}>
      <div
        className="mx-4 max-w-2xl w-full max-h-[90vh] overflow-y-auto"
        onClick={(e) => e.stopPropagation()}
      >
        <FormCard isVisible={true} borderColor="purple" className="shadow-2xl">
          <h3 className="text-xl font-bold text-white mb-4 flex items-center gap-2">
            <svg className="w-6 h-6 text-purple-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
            </svg>
            Generuj fiszki z tekstu (AI)
          </h3>

          {!showPreview ? (
            <div className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-300 mb-1">Tekst źródłowy *</label>
              <textarea
                value={text}
                onChange={(e) => setText(e.target.value)}
                className="w-full bg-gray-700/50 border border-gray-600 rounded-xl px-4 py-3 text-white placeholder-gray-500 focus:outline-none focus:border-purple-500/50 resize-none"
                rows={8}
                placeholder="Wklej tekst, z którego chcesz wygenerować fiszki (min. 200 znaków)..."
              />
              <p className="text-xs text-gray-500 mt-1">{text.length} / 10 000 znaków</p>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="relative">
                <TextEdit
                  label="Grupa (opcjonalnie)"
                  id="generateGroupName"
                  name="groupName"
                  type="text"
                  value={groupName}
                  onChange={(e) => { setGroupName(e.target.value); setShowGroupSuggestions(true); }}
                  placeholder="Np. Biologia..."
                  error={undefined}
                />
                {showGroupSuggestions && filteredGroups.length > 0 && (
                  <div className="absolute z-10 w-full mt-1 bg-gray-700 border border-gray-600 rounded-xl shadow-lg max-h-32 overflow-y-auto">
                    {filteredGroups.map((g) => (
                      <button
                        key={g}
                        type="button"
                        onMouseDown={() => { setGroupName(g); setShowGroupSuggestions(false); }}
                        className="w-full text-left px-4 py-2 text-sm text-gray-300 hover:bg-gray-600 hover:text-white transition-colors first:rounded-t-xl last:rounded-b-xl"
                      >
                        {g}
                      </button>
                    ))}
                  </div>
                )}
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-300 mb-1">Liczba fiszek</label>
                <input
                  type="number"
                  value={count}
                  onChange={(e) => setCount(Math.min(20, Math.max(1, Number(e.target.value))))}
                  min={1}
                  max={20}
                  className="w-full bg-gray-700/50 border border-gray-600 rounded-xl px-4 py-3 text-white focus:outline-none focus:border-purple-500/50"
                />
              </div>
            </div>

            {error && (
              <div className="bg-red-500/10 border border-red-500/30 rounded-xl px-4 py-3 text-red-400 text-sm break-words whitespace-pre-wrap">
                {error}
              </div>
            )}

            <div className="flex gap-3">
              <ButtonSecondary onClick={onCancel} className="flex-1">
                Anuluj
              </ButtonSecondary>
              <button
                onClick={handleGenerate}
                disabled={loading}
                className="flex-1 px-4 py-2.5 bg-gradient-to-r from-purple-500 to-pink-500 hover:from-purple-600 hover:to-pink-600 disabled:opacity-50 disabled:cursor-not-allowed text-white font-semibold rounded-xl transition-all duration-200 shadow-lg shadow-purple-500/25 flex items-center justify-center gap-2"
              >
                {loading ? (
                  <>
                    <svg className="animate-spin w-5 h-5" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" />
                    </svg>
                    Generowanie...
                  </>
                ) : (
                  <>
                    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
                    </svg>
                    Generuj
                  </>
                )}
              </button>
            </div>
          </div>
        ) : (
          <div className="space-y-4">
            <p className="text-gray-400 text-sm">
              Wygenerowano {generated.length} fiszek. Możesz edytować lub usunąć przed zapisem.
            </p>

            <div className="space-y-3 max-h-[50vh] overflow-y-auto pr-1">
              {generated.map((item, index) => (
                <div key={index} className="bg-gray-700/50 border border-gray-600 rounded-xl p-4">
                  <div className="flex items-start justify-between mb-2">
                    <span className="text-xs font-medium text-purple-400">Fiszka {index + 1}</span>
                    <button
                      onClick={() => handleRemoveGenerated(index)}
                      className="p-1 rounded-lg hover:bg-red-500/20 text-red-400 transition-colors"
                      title="Usuń"
                    >
                      <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
                      </svg>
                    </button>
                  </div>
                  <div className="space-y-2">
                    <div>
                      <label className="text-xs text-gray-400">Pytanie:</label>
                      <textarea
                        value={item.question}
                        onChange={(e) => handleEditGenerated(index, 'question', e.target.value)}
                        className="w-full bg-gray-600/50 border border-gray-500 rounded-lg px-3 py-2 text-white text-sm resize-none mt-1"
                        rows={2}
                      />
                    </div>
                    <div>
                      <label className="text-xs text-gray-400">Odpowiedź:</label>
                      <textarea
                        value={item.answer}
                        onChange={(e) => handleEditGenerated(index, 'answer', e.target.value)}
                        className="w-full bg-gray-600/50 border border-gray-500 rounded-lg px-3 py-2 text-white text-sm resize-none mt-1"
                        rows={2}
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {generated.length === 0 && (
              <p className="text-gray-500 text-center py-4">Wszystkie fiszki zostały usunięte.</p>
            )}

            <div className="flex gap-3">
              <ButtonSecondary onClick={() => setShowPreview(false)} className="flex-1">
                Wróć
              </ButtonSecondary>
              <ButtonPrimary
                onClick={handleSaveAll}
                disabled={generated.length === 0}
                className="flex-1"
              >
                Dodaj wszystkie ({generated.length})
              </ButtonPrimary>
            </div>
          </div>
        )}
        </FormCard>
      </div>
    </div>
  );
}

// ─── FlashcardsPage ─────────────────────────────────────────────

function FlashcardsPage() {
  const [isVisible, setIsVisible] = useState(false);
  const [flashcards, setFlashcards] = useState<Flashcard[]>([]);
  const [selectedGroup, setSelectedGroup] = useState<string>('');
  const [groups, setGroups] = useState<string[]>([]);
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  // Modals
  const [showAddModal, setShowAddModal] = useState(false);
  const [editFlashcard, setEditFlashcard] = useState<Flashcard | null>(null);
  const [showGenerateModal, setShowGenerateModal] = useState(false);
  const [deleteId, setDeleteId] = useState<string | null>(null);
  const [showDeleteAllModal, setShowDeleteAllModal] = useState(false);

  useEffect(() => {
    document.title = 'Fiszki | tomsoft1 workspace';
    const timer = setTimeout(() => setIsVisible(true), 100);

    const token = localStorage.getItem('token');
    const expiresAt = localStorage.getItem('tokenExpiresAt');
    if (token && expiresAt && new Date(expiresAt) > new Date()) {
      setIsLoggedIn(true);
    }

    loadFlashcards();

    return () => clearTimeout(timer);
  }, []);

  const loadFlashcards = () => {
    const all = flashcardsService.getAll();
    setFlashcards(all);
    const grps = flashcardsService.getGroups().map((g) => g.name);
    setGroups(grps);
  };

  const displayedFlashcards = selectedGroup
    ? flashcards.filter((f) => f.groupName === selectedGroup)
    : flashcards;

  const handleSaveFlashcard = (data: { question: string; answer: string; groupName?: string }) => {
    if (editFlashcard) {
      flashcardsService.update(editFlashcard.id, data);
    } else {
      flashcardsService.add({ ...data, source: 'manual' });
    }
    setShowAddModal(false);
    setEditFlashcard(null);
    loadFlashcards();
  };

  const handleEdit = (flashcard: Flashcard) => {
    setEditFlashcard(flashcard);
    setShowAddModal(true);
  };

  const handleDeleteConfirm = () => {
    if (deleteId) {
      flashcardsService.remove(deleteId);
      setDeleteId(null);
      loadFlashcards();
    }
  };

  const handleGenerateSave = (items: { question: string; answer: string; groupName?: string }[]) => {
    flashcardsService.addBatch(items.map((item) => ({ ...item, source: 'ai' as const })));
    setShowGenerateModal(false);
    loadFlashcards();
  };

  const handleDeleteAll = () => {
    flashcardsService.clear();
    setShowDeleteAllModal(false);
    loadFlashcards();
  };

  return (
    <section className="min-h-[calc(100vh-4rem)] px-4 py-16 overflow-hidden">
      <div className="max-w-7xl mx-auto">
        {/* Header */}
        <div className={`text-center mb-10 transition-all duration-700 ease-out ${isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'}`}>
          <h1 className="text-4xl md:text-5xl font-bold mb-3">
            <span className="bg-gradient-to-r from-amber-400 via-yellow-500 to-orange-400 bg-clip-text text-transparent">
              Fiszki
            </span>
          </h1>
          <p className="text-gray-400 text-lg">Twórz fiszki i ucz się efektywnie</p>
        </div>

        {/* Toolbar */}
        <div className={`flex flex-wrap items-center justify-between gap-4 mb-8 transition-all duration-700 ease-out ${isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'}`}
          style={{ transitionDelay: '150ms' }}
        >
          <div className="flex items-center gap-4">
            {/* Filter */}
            <div className="flex items-center gap-2">
              <select
                value={selectedGroup}
                onChange={(e) => setSelectedGroup(e.target.value)}
                className="bg-gray-800/80 border border-gray-700/50 rounded-xl px-4 py-2.5 text-white text-sm focus:outline-none focus:border-cyan-500/50"
              >
                <option value="">Wszystkie grupy</option>
                {groups.map((g) => (
                  <option key={g} value={g}>{g}</option>
                ))}
              </select>
            </div>

            {/* Counter */}
            <span className="text-gray-500 text-sm">
              {displayedFlashcards.length} {displayedFlashcards.length === 1 ? 'fiszka' : displayedFlashcards.length < 5 ? 'fiszki' : 'fiszek'}
              {flashcardsService.isNearLimit() && (
                <span className="text-amber-400 ml-2">(zbliżasz się do limitu 500)</span>
              )}
            </span>
          </div>

          <div className="flex items-center gap-3">
            {/* Delete all button */}
            <ButtonDanger onClick={() => setShowDeleteAllModal(true)} disabled={flashcards.length === 0}>
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
              </svg>
              Usuń wszystkie fiszki
            </ButtonDanger>

            {/* Generate button */}
            <div className="relative group/gen">
              <ButtonSecondary onClick={() => isLoggedIn && setShowGenerateModal(true)} disabled={!isLoggedIn}>
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z" />
                </svg>
                Generuj z tekstu
              </ButtonSecondary>
              {!isLoggedIn && (
                <div className="absolute bottom-full left-1/2 -translate-x-1/2 mb-2 px-3 py-1.5 bg-gray-900 text-gray-300 text-xs rounded-lg opacity-0 group-hover/gen:opacity-100 transition-opacity whitespace-nowrap pointer-events-none border border-gray-700">
                  Zaloguj się aby generować fiszki
                </div>
              )}
            </div>

            {/* Add button */}
            <ButtonPrimary onClick={() => { setEditFlashcard(null); setShowAddModal(true); }}>
              <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
              </svg>
              Dodaj fiszkę
            </ButtonPrimary>
          </div>
        </div>

        {/* Grid */}
        {displayedFlashcards.length === 0 ? (
          <div className={`text-center py-20 transition-all duration-700 ease-out ${isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'}`}
            style={{ transitionDelay: '300ms' }}
          >
            <div className="w-20 h-20 mx-auto mb-6 rounded-2xl bg-amber-500/10 flex items-center justify-center">
              <svg className="w-10 h-10 text-amber-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10" />
              </svg>
            </div>
            <h3 className="text-xl font-bold text-white mb-2">Brak fiszek</h3>
            <p className="text-gray-400 mb-6">Dodaj swoją pierwszą fiszkę lub wygeneruj fiszki z tekstu!</p>
            <ButtonPrimary onClick={() => { setEditFlashcard(null); setShowAddModal(true); }}>
              Dodaj pierwszą fiszkę
            </ButtonPrimary>
          </div>
        ) : (
          <div className={`grid grid-cols-1 md:grid-cols-2 gap-6 transition-all duration-700 ease-out ${isVisible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-8'}`}
            style={{ transitionDelay: '300ms' }}
          >
            {displayedFlashcards.map((flashcard, index) => (
              <div
                key={flashcard.id}
                className="transition-all duration-500 ease-out"
                style={{
                  transitionDelay: isVisible ? `${300 + index * 50}ms` : '0ms',
                  opacity: isVisible ? 1 : 0,
                  transform: isVisible ? 'translateY(0)' : 'translateY(20px)',
                }}
              >
                <FlashcardItem
                  flashcard={flashcard}
                  onEdit={handleEdit}
                  onDelete={(id) => setDeleteId(id)}
                />
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Modals */}
      <FlashcardModal
        isOpen={showAddModal}
        flashcard={editFlashcard}
        groups={groups}
        onSave={handleSaveFlashcard}
        onCancel={() => { setShowAddModal(false); setEditFlashcard(null); }}
      />

      <GenerateModal
        isOpen={showGenerateModal}
        groups={groups}
        onSave={handleGenerateSave}
        onCancel={() => setShowGenerateModal(false)}
      />

      <ConfirmModal
        isOpen={deleteId !== null}
        title="Usuń fiszkę"
        message="Czy na pewno chcesz usunąć tę fiszkę? Tej operacji nie można cofnąć."
        confirmText="Usuń"
        cancelText="Anuluj"
        variant="danger"
        onConfirm={handleDeleteConfirm}
        onCancel={() => setDeleteId(null)}
      />

      <ConfirmModal
        isOpen={showDeleteAllModal}
        title="Usuń wszystkie fiszki"
        message={`Czy na pewno chcesz usunąć wszystkie fiszki (${flashcards.length})? Tej operacji nie można cofnąć.`}
        confirmText="Usuń wszystkie"
        cancelText="Anuluj"
        variant="danger"
        onConfirm={handleDeleteAll}
        onCancel={() => setShowDeleteAllModal(false)}
      />
    </section>
  );
}

export default FlashcardsPage;
