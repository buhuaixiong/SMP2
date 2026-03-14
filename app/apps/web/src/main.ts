import { createApp } from "vue";
import { createPinia } from "pinia";
import App from "./App.vue";
import router from "./router";
import i18n from "./i18n";
import { useLocaleStore } from "./stores/locale";
import { installServiceManager } from "@/core/services";
import { registerServices } from "@/services";
import { permissionDirective, roleDirective } from "@/directives/permission";
import "./assets/main.css";
import "./assets/notifications.css";

const bootstrap = async () => {
  const app = createApp(App);

  const pinia = createPinia();
  app.use(pinia);
  app.use(router);
  app.use(i18n);

  registerServices();
  const serviceManager = installServiceManager(app);
  app.directive("permission", permissionDirective);
  app.directive("role", roleDirective);

  const localeStore = useLocaleStore();
  await localeStore.initialize();

  await serviceManager.startAll();

  app.mount("#app");
};

void bootstrap();
