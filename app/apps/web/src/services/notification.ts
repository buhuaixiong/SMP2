import { ElMessage, ElMessageBox, ElNotification } from "element-plus";
import type { NotificationOptions, NotificationParams } from "element-plus";
import type { MessageParams } from "element-plus/es/components/message";
import type { ElMessageBoxOptions, MessageBoxData } from "element-plus/es/components/message-box";
import type { ServiceDefinition } from "@/core/services";

type NotificationType = "success" | "warning" | "info" | "error";
type NotificationMessage = NotificationOptions["message"];

export interface NotificationService {
  // success method overloads
  success(message: string, title?: string, options?: Partial<NotificationOptions>): void;
  success(options: NotificationParams): void;

  // warning method overloads
  warning(message: string, title?: string, options?: Partial<NotificationOptions>): void;
  warning(options: NotificationParams): void;

  // info method overloads
  info(message: string, title?: string, options?: Partial<NotificationOptions>): void;
  info(options: NotificationParams): void;

  // error method overloads
  error(message: string, title?: string, options?: Partial<NotificationOptions> & { sticky?: boolean }): void;
  error(options: NotificationParams & { sticky?: boolean }): void;

  notify(options: NotificationParams): void;

  // message method overloads
  message(text: string, type?: NotificationType, options?: MessageParams): void;
  message(options: MessageParams): void;

  confirm(message: string, title?: string, options?: ElMessageBoxOptions): Promise<MessageBoxData>;
  prompt(
    message: string,
    title?: string,
    options?: ElMessageBoxOptions & { inputPattern?: RegExp; inputErrorMessage?: string },
  ): Promise<{ value: string } & MessageBoxData>;
  alert(message: string, title?: string, options?: ElMessageBoxOptions): Promise<MessageBoxData>;
}

// 默认配置
const DEFAULT_DURATION = 4500;
const ICONS: Record<NotificationType, string> = {
  success: "Check",
  warning: "Warning",
  error: "Close",
  info: "InfoFilled",
};

// 带进度条的 HTML 模板
const createProgressBarHTML = (duration: number, type: NotificationType): string => {
  return `
    <div class="el-notification__progress" style="position: absolute; bottom: 0; left: 0; width: 100%; height: 3px; background: transparent; overflow: hidden;">
      <div class="el-notification__progress-bar" style="
        height: 100%;
        background: ${getProgressColor(type)};
        width: 100%;
        animation: progress-shrink ${duration}ms linear forwards;
        transform-origin: left;
      "></div>
    </div>
    <style>
      @keyframes progress-shrink {
        from { transform: scaleX(1); }
        to { transform: scaleX(0); }
      }
      .el-notification:hover .el-notification__progress-bar {
        animation-play-state: paused !important;
      }
    </style>
  `;
};

const getProgressColor = (type: NotificationType): string => {
  const colors: Record<NotificationType, string> = {
    success: "linear-gradient(90deg, #10b981 0%, #34d399 100%)",
    warning: "linear-gradient(90deg, #f59e0b 0%, #fbbf24 100%)",
    error: "linear-gradient(90deg, #ef4444 0%, #f87171 100%)",
    info: "linear-gradient(90deg, #3b82f6 0%, #60a5fa 100%)",
  };
  return colors[type];
};

const withDefaults = (
  type: NotificationType,
  message: string,
  title?: string,
  options: Partial<NotificationOptions> = {},
): NotificationOptions => {
  const duration = options.duration ?? DEFAULT_DURATION;

  // 如果不是 sticky 类型，添加进度条
  let customHtml = "";
  if (duration > 0 && !options.showClose) {
    // 只有在不显示关闭按钮时才添加进度条
  }

  return {
    ...options,
    type,
    title: title || options.title || "",
    message,
    duration,
    // 自定义图标
    icon: options.icon ?? ICONS[type],
    showClose: options.showClose ?? true,
  } as NotificationOptions;
};

// 创建带进度条的通知
const createNotificationWithProgress = (
  type: NotificationType,
  message: NotificationMessage,
  title: string | undefined,
  options: Partial<NotificationOptions> = {},
): void => {
  const duration = options.duration ?? DEFAULT_DURATION;

  // 构建通知配置
  const config = {
    ...options,
    customClass: options.customClass ?? "",
    dangerouslyUseHTMLString: options.dangerouslyUseHTMLString ?? false,
    position: options.position ?? "top-right",
    offset: options.offset ?? 0,
    type,
    title: title || options.title || "",
    message: message ?? "",
    duration,
    icon: options.icon ?? ICONS[type],
    showClose: options.showClose ?? true,
  };

  // 打开通知
  const notificationInstance = ElNotification(config);

  // 如果 duration > 0 且不是 sticky，添加进度条
  if (duration > 0 && !config.showClose) {
    // 等待 DOM 更新后添加进度条
    setTimeout(() => {
      const notificationEl = document.querySelector(".el-notification");
      if (notificationEl && !notificationEl.querySelector(".el-notification__progress")) {
        const progressBar = document.createElement("div");
        progressBar.className = "el-notification__progress";
        progressBar.innerHTML = `
          <div class="el-notification__progress-bar" style="
            height: 100%;
            background: ${getProgressColor(type)};
            width: 100%;
            animation: progress-shrink ${duration}ms linear forwards;
          "></div>
        `;
        notificationEl.appendChild(progressBar);

        // 添加暂停动画的样式
        const style = document.createElement("style");
        style.textContent = `
          @keyframes progress-shrink {
            from { transform: scaleX(1); }
            to { transform: scaleX(0); }
          }
          .el-notification:hover .el-notification__progress-bar {
            animation-play-state: paused !important;
          }
        `;
        notificationEl.appendChild(style);
      }
    }, 50);
  }
};

export const notificationService: ServiceDefinition<NotificationService> = {
  name: "notification",
  setup() {
    return {
      success(messageOrOptions: string | NotificationParams, title?: string, options?: Partial<NotificationOptions>): void {
        if (typeof messageOrOptions === 'string') {
          createNotificationWithProgress("success", messageOrOptions, title, options);
        } else {
          const params = messageOrOptions as any;
          createNotificationWithProgress("success", params.message, params.title || params.title, {
            ...params,
            type: 'success',
            duration: params.duration ?? DEFAULT_DURATION,
          });
        }
      },

      warning(messageOrOptions: string | NotificationParams, title?: string, options?: Partial<NotificationOptions>): void {
        if (typeof messageOrOptions === 'string') {
          createNotificationWithProgress("warning", messageOrOptions, title, options);
        } else {
          const params = messageOrOptions as any;
          createNotificationWithProgress("warning", params.message, params.title || params.title, {
            ...params,
            type: 'warning',
            duration: params.duration ?? DEFAULT_DURATION,
          });
        }
      },

      info(messageOrOptions: string | NotificationParams, title?: string, options?: Partial<NotificationOptions>): void {
        if (typeof messageOrOptions === 'string') {
          createNotificationWithProgress("info", messageOrOptions, title, options);
        } else {
          const params = messageOrOptions as any;
          createNotificationWithProgress("info", params.message, params.title || params.title, {
            ...params,
            type: 'info',
            duration: params.duration ?? DEFAULT_DURATION,
          });
        }
      },

      error(
        messageOrOptions: string | (NotificationParams & { sticky?: boolean }),
        title?: string,
        options?: Partial<NotificationOptions> & { sticky?: boolean }
      ): void {
        if (typeof messageOrOptions === 'string') {
          const duration = options?.sticky ? 0 : (options?.duration ?? DEFAULT_DURATION);
          const showClose = options?.sticky ? (options.showClose ?? true) : options?.showClose;

          createNotificationWithProgress("error", messageOrOptions, title, {
            ...options,
            type: "error" as const,
            duration,
            showClose,
          });
        } else {
          const params = messageOrOptions as any;
          const duration = params.sticky ? 0 : (params.duration ?? DEFAULT_DURATION);
          const showClose = params.sticky ? (params.showClose ?? true) : params.showClose;

          createNotificationWithProgress("error", params.message, params.title || params.title, {
            ...params,
            type: 'error',
            duration,
            showClose,
          });
        }
      },

      notify(options: NotificationParams): void {
        if (typeof options === "string") {
          createNotificationWithProgress("info", options, undefined, {});
          return;
        }

        if (!("message" in options)) {
          createNotificationWithProgress("info", options as NotificationMessage, undefined, {});
          return;
        }

        const type = (options.type as NotificationType) || "info";
        createNotificationWithProgress(
          type,
          options.message ?? "",
          options.title || options.title,
          options,
        );
      },

      message(textOrOptions: string | MessageParams, type: NotificationType = "info", options?: MessageParams): void {
        if (typeof textOrOptions === 'string') {
          if (options) {
            ElMessage({
              message: textOrOptions,
              type,
              ...(options as Record<string, any>),
            });
          } else {
            ElMessage({
              message: textOrOptions,
              type,
            });
          }
        } else {
          ElMessage(textOrOptions);
        }
      },

      confirm(message: string, title = "确认操作", options: ElMessageBoxOptions = {}): Promise<MessageBoxData> {
        return ElMessageBox.confirm(message, title, {
          type: options.type ?? "warning",
          confirmButtonText: options.confirmButtonText ?? "确定",
          cancelButtonText: options.cancelButtonText ?? "取消",
          ...options,
        });
      },

      prompt(message: string, title = "请输入内容", options: ElMessageBoxOptions & { inputPattern?: RegExp; inputErrorMessage?: string } = {}): Promise<{ value: string } & MessageBoxData> {
        return ElMessageBox.prompt(message, title, {
          confirmButtonText: options.confirmButtonText ?? "确定",
          cancelButtonText: options.cancelButtonText ?? "取消",
          inputType: options.inputType ?? "textarea",
          inputPattern: options.inputPattern,
          inputErrorMessage: options.inputErrorMessage ?? "请输入有效内容",
          ...options,
        });
      },

      alert(message: string, title = "提示", options: ElMessageBoxOptions = {}): Promise<MessageBoxData> {
        return ElMessageBox.alert(message, title, {
          confirmButtonText: options.confirmButtonText ?? "确定",
          ...options,
        });
      },
    };
  },
};
