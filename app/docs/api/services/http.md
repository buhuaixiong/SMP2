# HttpService API

文件：`apps/web/src/services/http.ts`

```ts
export interface HttpService {
  request<T = any>(config: HttpRequestConfig): Promise<T>
  get<T = any>(url: string, config?: HttpRequestConfig): Promise<T>
  post<T = any>(url: string, data?: any, config?: HttpRequestConfig): Promise<T>
  put<T = any>(url: string, data?: any, config?: HttpRequestConfig): Promise<T>
  delete<T = any>(url: string, config?: HttpRequestConfig): Promise<T>
  fetch<T = any>(url: string, init?: ApiFetchInit): Promise<T>
}
```

`HttpRequestConfig` 继承自 AxiosConfig，新增 `silent?: boolean` 以禁用通知。

功能特性：

- 请求拦截器：自动注入 `Authorization Bearer`；强制 HTTPS（prod/forceHttps 时）。  
- 响应拦截：401 自动跳转登录、403 输出 warning、503 支持 lockdown store。  
- `fetch` 封装现有 `apiFetch`，兼容历史调用方式。

### 示例

```ts
const http = useService<HttpService>("http")
const suppliers = await http.get("/suppliers", { params: { q: keyword }, silent: true })
```
