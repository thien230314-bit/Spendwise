import { useEffect, useRef, useState } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  Pressable,
  ScrollView,
  StyleSheet,
  Text,
  TextInput,
  View,
} from 'react-native';
import { AppScreen } from '@/components/AppScreen';
import { Card, Copy, EmptyState, SectionTitle, Skeleton, UIIcon } from '@/components/MobileUI';
import { aiApi, type AiMessageDto } from '@/api/aiApi';
import { fontStyles, radius, spacing } from '@/theme/tokens';
import { useTheme } from '@/theme/useTheme';

export default function AiChatScreen() {
  const { colors } = useTheme();
  const [messages, setMessages] = useState<AiMessageDto[]>([
    {
      id: 'welcome',
      role: 'assistant',
      content:
        'Xin chào! Tôi là Trợ lý AI SpendWise. Tôi có thể giúp bạn phân tích chi tiêu, tư vấn cân đối ngân sách và gợi ý kế hoạch tài chính cá nhân.',
      createdAt: new Date().toISOString(),
    },
  ]);
  const [inputText, setInputText] = useState('');
  const [loading, setLoading] = useState(false);
  const [conversationId, setConversationId] = useState<string | null>(null);
  const scrollRef = useRef<ScrollView>(null);

  useEffect(() => {
    // Check or load existing conversation
    aiApi
      .getConversations()
      .then((convs) => {
        if (convs && convs.length > 0) {
          const latest = convs[0];
          setConversationId(latest.id);
          if (latest.messages && latest.messages.length > 0) {
            setMessages(latest.messages);
          }
        }
      })
      .catch(() => {});
  }, []);

  const handleSend = async () => {
    const text = inputText.trim();
    if (!text || loading) return;

    const userMsg: AiMessageDto = {
      id: `user-${Date.now()}`,
      role: 'user',
      content: text,
      createdAt: new Date().toISOString(),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInputText('');
    setLoading(true);

    setTimeout(() => {
      scrollRef.current?.scrollToEnd({ animated: true });
    }, 100);

    try {
      const response = await aiApi.sendMessage({
        conversationId,
        message: text,
      });

      if (response.conversationId) {
        setConversationId(response.conversationId);
      }

      const assistantMsg: AiMessageDto = {
        id: `ai-${Date.now()}`,
        role: 'assistant',
        content: response.message,
        createdAt: new Date().toISOString(),
      };

      setMessages((prev) => [...prev, assistantMsg]);
    } catch (err: any) {
      const errorMsg: AiMessageDto = {
        id: `err-${Date.now()}`,
        role: 'assistant',
        content: err?.message || 'Trợ lý AI hiện đang phản hồi chậm, vui lòng thử lại sau giây lát.',
        createdAt: new Date().toISOString(),
      };
      setMessages((prev) => [...prev, errorMsg]);
    } finally {
      setLoading(false);
      setTimeout(() => {
        scrollRef.current?.scrollToEnd({ animated: true });
      }, 150);
    }
  };

  return (
    <AppScreen title="Trợ lý AI SpendWise" subtitle="Hỏi đáp và phân tích tài chính thông minh.">
      <KeyboardAvoidingView
        behavior={Platform.OS === 'ios' ? 'padding' : undefined}
        style={styles.keyboardView}
      >
        {/* Chat History Container */}
        <Card>
          <ScrollView
            ref={scrollRef}
            contentContainerStyle={styles.messagesList}
            nestedScrollEnabled
            showsVerticalScrollIndicator={false}
          >
            {messages.map((m) => {
              const isUser = m.role === 'user';
              return (
                <View
                  key={m.id}
                  style={[
                    styles.messageBubble,
                    isUser
                      ? [styles.userBubble, { backgroundColor: colors.primary }]
                      : [styles.assistantBubble, { backgroundColor: colors.surfaceElevated, borderColor: colors.border }],
                  ]}
                >
                  {!isUser && (
                    <View style={styles.assistantHeader}>
                      <UIIcon name="ai" size={16} color={colors.primary} />
                      <Text style={[styles.assistantName, { color: colors.primary }]}>SpendWise AI</Text>
                    </View>
                  )}
                  <Text
                    style={[
                      styles.messageText,
                      { color: isUser ? colors.onButton : colors.text },
                    ]}
                  >
                    {m.content}
                  </Text>
                </View>
              );
            })}

            {loading && (
              <View
                style={[
                  styles.messageBubble,
                  styles.assistantBubble,
                  { backgroundColor: colors.surfaceElevated, borderColor: colors.border },
                ]}
              >
                <View style={styles.assistantHeader}>
                  <View style={{ width: 8, height: 8, borderRadius: 4, backgroundColor: colors.primary }} />
                  <Text style={[styles.assistantName, { color: colors.primary }]}>Đang phân tích dữ liệu...</Text>
                </View>
              </View>
            )}
          </ScrollView>

          {/* Prompt Suggestions */}
          <View style={styles.suggestionsRow}>
            {['Chi tiêu tháng này thế nào?', 'Gợi ý tiết kiệm', 'Phân tích ngân sách'].map((suggestion) => (
              <Pressable
                key={suggestion}
                accessibilityRole="button"
                onPress={() => setInputText(suggestion)}
                style={[styles.suggestionChip, { borderColor: colors.border, backgroundColor: colors.muted }]}
              >
                <Text style={[styles.suggestionText, { color: colors.textMuted }]}>{suggestion}</Text>
              </Pressable>
            ))}
          </View>

          {/* Input Box */}
          <View style={[styles.inputContainer, { borderColor: colors.border, backgroundColor: colors.input }]}>
            <TextInput
              value={inputText}
              onChangeText={setInputText}
              placeholder="Nhập câu hỏi tài chính của bạn…"
              placeholderTextColor={colors.textMuted}
              returnKeyType="send"
              onSubmitEditing={handleSend}
              style={[styles.input, { color: colors.text }]}
              multiline
            />
            <Pressable
              accessibilityRole="button"
              onPress={handleSend}
              disabled={loading || !inputText.trim()}
              style={[
                styles.sendBtn,
                {
                  backgroundColor: colors.primary,
                  opacity: loading || !inputText.trim() ? 0.5 : 1,
                },
              ]}
            >
              <UIIcon name="ai" size={18} color={colors.onButton} />
            </Pressable>
          </View>
        </Card>
      </KeyboardAvoidingView>
    </AppScreen>
  );
}

const styles = StyleSheet.create({
  keyboardView: { flex: 1 },
  messagesList: { gap: spacing.md, paddingVertical: spacing.sm, minHeight: 280, maxHeight: 420 },
  messageBubble: { padding: spacing.md, borderRadius: radius.lg, maxWidth: '85%' },
  userBubble: { alignSelf: 'flex-end', borderBottomRightRadius: radius.sm },
  assistantBubble: { alignSelf: 'flex-start', borderWidth: 1, borderBottomLeftRadius: radius.sm, gap: spacing.xs },
  assistantHeader: { flexDirection: 'row', alignItems: 'center', gap: spacing.xs },
  assistantName: { fontSize: 11, ...fontStyles.semibold },
  messageText: { fontSize: 13, lineHeight: 20, ...fontStyles.body },
  suggestionsRow: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs, paddingTop: spacing.xs },
  suggestionChip: { paddingHorizontal: spacing.sm, paddingVertical: 5, borderRadius: radius.pill, borderWidth: 1 },
  suggestionText: { fontSize: 11, ...fontStyles.body },
  inputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    borderWidth: 1,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.xs,
    minHeight: 50,
  },
  input: { flex: 1, fontSize: 13, maxHeight: 90, ...fontStyles.body },
  sendBtn: { width: 36, height: 36, borderRadius: radius.sm, alignItems: 'center', justifyContent: 'center' },
});
