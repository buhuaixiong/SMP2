import { ref, Ref } from "vue";
import { getDashboardTodos } from "@/api/dashboard";
import type { TodoItem } from "@/api/dashboard";

interface UseTodoItemsReturn {
  todos: Ref<TodoItem[]>;
  loading: Ref<boolean>;
  fetchTodos: () => Promise<void>;
  refreshTodos: () => Promise<void>;
}

export function useTodoItems(): UseTodoItemsReturn {
  const todos = ref<TodoItem[]>([]);
  const loading = ref(false);

  async function fetchTodos() {
    loading.value = true;
    try {
      const todosData = await getDashboardTodos();
      todos.value = todosData ?? [];
    } catch (error) {
      console.error("Failed to fetch dashboard todos:", error);
      throw error;
    } finally {
      loading.value = false;
    }
  }

  async function refreshTodos() {
    await fetchTodos();
  }

  return {
    todos,
    loading,
    fetchTodos,
    refreshTodos,
  };
}
