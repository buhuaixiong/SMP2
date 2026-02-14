/**
 * 存储服务接口定义
 *
 * 统一文件存储抽象层,支持多种存储后端
 * - Local: 本地文件系统
 * - S3: AWS S3
 * - MinIO: 自建 S3 兼容存储
 * - OSS: 阿里云对象存储
 * - COS: 腾讯云对象存储
 *
 * @version 1.0
 * @date 2025-10-29
 */

// =====================================================================
// 类型定义
// =====================================================================

/**
 * 存储后端类型
 */
export type StorageBackend = 'local' | 's3' | 'minio' | 'oss' | 'cos';

/**
 * 文件分类
 */
export type FileCategory =
  | 'documents'          // 一般文档
  | 'contracts'          // 合同
  | 'invoices'           // 发票
  | 'licenses'           // 证照
  | 'certificates'       // 证书
  | 'images'             // 图片
  | 'temp'               // 临时文件
  | 'backups'            // 备份
  | 'exports'            // 导出文件
  | 'templates'          // 模板
  | 'other';             // 其他

/**
 * MIME 类型常量
 */
export enum MimeType {
  PDF = 'application/pdf',
  WORD = 'application/msword',
  WORD_DOCX = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  EXCEL = 'application/vnd.ms-excel',
  EXCEL_XLSX = 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  CSV = 'text/csv',
  JSON = 'application/json',
  XML = 'application/xml',
  ZIP = 'application/zip',
  JPEG = 'image/jpeg',
  PNG = 'image/png',
  GIF = 'image/gif',
  SVG = 'image/svg+xml',
  TEXT = 'text/plain',
  HTML = 'text/html',
  OCTET_STREAM = 'application/octet-stream'
}

/**
 * 文件元数据
 */
export interface FileMetadata {
  /** 原始文件名 */
  originalName: string;

  /** 文件分类 */
  category: FileCategory;

  /** 供应商 ID (可选) */
  supplierId?: number;

  /** 上传用户 ID (可选) */
  userId?: number;

  /** MIME 类型 (可选,会自动检测) */
  mimeType?: string;

  /** 标签 (可选) */
  tags?: string[];

  /** 过期时间 (可选) */
  expiresAt?: Date;

  /** 自定义元数据 (可选) */
  customMetadata?: Record<string, string | number | boolean>;

  /** 是否为敏感文件 (可选) */
  isSensitive?: boolean;

  /** 关联实体类型 (可选) */
  entityType?: string;

  /** 关联实体 ID (可选) */
  entityId?: number;
}

/**
 * 文件信息
 */
export interface FileInfo {
  /** 文件唯一 ID (UUID) */
  fileId: string;

  /** 原始文件名 */
  originalName: string;

  /** 文件大小 (字节) */
  size: number;

  /** MIME 类型 */
  mimeType: string;

  /** 上传时间 */
  uploadedAt: Date;

  /** 上传用户 ID */
  uploadedBy?: number;

  /** 存储位置 */
  storageLocation: StorageBackend;

  /** S3/MinIO/OSS 对象键 (如果使用对象存储) */
  s3Key?: string;

  /** 本地路径 (如果使用本地存储) */
  filePath?: string;

  /** 文件分类 */
  category?: FileCategory;

  /** 供应商 ID */
  supplierId?: number;

  /** 文件元数据 */
  metadata?: Partial<FileMetadata>;

  /** 访问 URL (已弃用,请使用 getSignedUrl) */
  @deprecated
  url?: string;

  /** 是否已加密 */
  encrypted?: boolean;

  /** 文件 ETag (对象存储) */
  etag?: string;

  /** 最后修改时间 */
  lastModified?: Date;
}

/**
 * 列表选项
 */
export interface ListOptions {
  /** 返回数量限制 (默认: 100) */
  limit?: number;

  /** 偏移量 (默认: 0) */
  offset?: number;

  /** 排序字段 (默认: uploadedAt) */
  sortBy?: 'originalName' | 'size' | 'uploadedAt';

  /** 排序顺序 (默认: desc) */
  sortOrder?: 'asc' | 'desc';

  /** 分页令牌 (用于大数据量) */
  continuationToken?: string;
}

/**
 * 上传选项
 */
export interface UploadOptions {
  /** 是否进行病毒扫描 (默认: true) */
  virusScan?: boolean;

  /** 是否生成缩略图 (图片文件,默认: false) */
  generateThumbnail?: boolean;

  /** 缩略图尺寸 (默认: { width: 200, height: 200 }) */
  thumbnailSize?: { width: number; height: number };

  /** 是否加密存储 (默认: false) */
  encrypt?: boolean;

  /** 是否覆盖同名文件 (默认: false) */
  overwrite?: boolean;

  /** 内容类型 (可选,覆盖自动检测) */
  contentType?: string;

  /** 内容编码 (可选) */
  contentEncoding?: string;

  /** 缓存控制 (可选) */
  cacheControl?: string;

  /** 内容处置 (attachment 或 inline) */
  contentDisposition?: 'attachment' | 'inline';

  /** ACL 权限 (对象存储) */
  acl?: 'private' | 'public-read' | 'public-read-write';

  /** 存储类 (S3 Storage Class) */
  storageClass?: 'STANDARD' | 'REDUCED_REDUNDANCY' | 'STANDARD_IA' | 'ONEZONE_IA' | 'INTELLIGENT_TIERING' | 'GLACIER' | 'DEEP_ARCHIVE';
}

/**
 * 下载选项
 */
export interface DownloadOptions {
  /** Range 请求 (部分下载) */
  range?: { start: number; end: number };

  /** 是否返回流 (而非 Buffer) */
  stream?: boolean;

  /** 超时时间 (毫秒,默认: 30000) */
  timeout?: number;
}

/**
 * 签名 URL 选项
 */
export interface SignedUrlOptions {
  /** 过期时间 (秒,默认: 3600) */
  expiresIn?: number;

  /** 文件名 (Content-Disposition) */
  filename?: string;

  /** 响应内容类型 (覆盖原始 MIME) */
  responseContentType?: string;

  /** 是否为下载链接 (attachment) */
  forceDownload?: boolean;
}

/**
 * 复制选项
 */
export interface CopyOptions {
  /** 目标分类 (默认: 与源文件相同) */
  targetCategory?: FileCategory;

  /** 目标供应商 ID (可选) */
  targetSupplierId?: number;

  /** 是否保留元数据 (默认: true) */
  preserveMetadata?: boolean;

  /** 新文件元数据 (可选) */
  newMetadata?: Partial<FileMetadata>;
}

/**
 * 删除选项
 */
export interface DeleteOptions {
  /** 是否硬删除 (默认: false,软删除) */
  permanent?: boolean;

  /** 是否删除所有版本 (版本控制场景) */
  deleteAllVersions?: boolean;
}

/**
 * 列表结果
 */
export interface ListResult {
  /** 文件列表 */
  files: FileInfo[];

  /** 总数 (如果可获取) */
  total?: number;

  /** 是否有更多结果 */
  hasMore: boolean;

  /** 下一页令牌 (用于分页) */
  nextContinuationToken?: string;
}

/**
 * 病毒扫描结果
 */
export interface VirusScanResult {
  /** 是否通过 */
  clean: boolean;

  /** 威胁名称 (如果检测到) */
  threat?: string;

  /** 扫描引擎 */
  engine: string;

  /** 扫描时间 */
  scannedAt: Date;
}

/**
 * 上传进度回调
 */
export type UploadProgressCallback = (progress: {
  loaded: number;
  total: number;
  percentage: number;
}) => void;

/**
 * 下载进度回调
 */
export type DownloadProgressCallback = (progress: {
  loaded: number;
  total: number;
  percentage: number;
}) => void;

// =====================================================================
// 存储服务接口
// =====================================================================

/**
 * 存储服务接口
 *
 * 所有存储适配器必须实现此接口
 */
export interface IStorageService {
  /**
   * 上传文件
   *
   * @param buffer 文件 Buffer
   * @param metadata 文件元数据
   * @param options 上传选项 (可选)
   * @param onProgress 进度回调 (可选)
   * @returns Promise<文件 ID>
   * @throws {ValidationError} 文件验证失败
   * @throws {VirusScanError} 病毒扫描失败
   * @throws {StorageError} 存储错误
   *
   * @example
   * ```typescript
   * const fileId = await storage.upload(
   *   buffer,
   *   {
   *     originalName: 'contract.pdf',
   *     category: 'contracts',
   *     supplierId: 123
   *   },
   *   { virusScan: true, encrypt: true }
   * );
   * ```
   */
  upload(
    buffer: Buffer,
    metadata: FileMetadata,
    options?: UploadOptions,
    onProgress?: UploadProgressCallback
  ): Promise<string>;

  /**
   * 批量上传文件
   *
   * @param files 文件数组 (Buffer + 元数据)
   * @param options 上传选项 (可选)
   * @returns Promise<文件 ID 数组>
   *
   * @example
   * ```typescript
   * const fileIds = await storage.uploadBatch([
   *   { buffer: buffer1, metadata: metadata1 },
   *   { buffer: buffer2, metadata: metadata2 }
   * ]);
   * ```
   */
  uploadBatch(
    files: Array<{ buffer: Buffer; metadata: FileMetadata }>,
    options?: UploadOptions
  ): Promise<string[]>;

  /**
   * 下载文件
   *
   * @param fileId 文件 ID
   * @param options 下载选项 (可选)
   * @param onProgress 进度回调 (可选)
   * @returns Promise<文件 Buffer>
   * @throws {NotFoundError} 文件不存在
   * @throws {StorageError} 下载错误
   *
   * @example
   * ```typescript
   * const buffer = await storage.download('file-uuid-here');
   * ```
   */
  download(
    fileId: string,
    options?: DownloadOptions,
    onProgress?: DownloadProgressCallback
  ): Promise<Buffer>;

  /**
   * 获取签名 URL (临时访问链接)
   *
   * @param fileId 文件 ID
   * @param options 签名选项 (可选)
   * @returns Promise<签名 URL>
   * @throws {NotFoundError} 文件不存在
   *
   * @example
   * ```typescript
   * const url = await storage.getSignedUrl('file-uuid', {
   *   expiresIn: 3600,
   *   forceDownload: true,
   *   filename: 'contract.pdf'
   * });
   * ```
   */
  getSignedUrl(
    fileId: string,
    options?: SignedUrlOptions
  ): Promise<string>;

  /**
   * 删除文件
   *
   * @param fileId 文件 ID
   * @param options 删除选项 (可选)
   * @returns Promise<是否成功>
   * @throws {NotFoundError} 文件不存在
   *
   * @example
   * ```typescript
   * await storage.delete('file-uuid', { permanent: true });
   * ```
   */
  delete(
    fileId: string,
    options?: DeleteOptions
  ): Promise<boolean>;

  /**
   * 批量删除文件
   *
   * @param fileIds 文件 ID 数组
   * @param options 删除选项 (可选)
   * @returns Promise<删除结果数组>
   */
  deleteBatch(
    fileIds: string[],
    options?: DeleteOptions
  ): Promise<Array<{ fileId: string; success: boolean; error?: string }>>;

  /**
   * 列出文件
   *
   * @param prefix 前缀过滤 (例如: "documents/2025/10/")
   * @param options 列表选项 (可选)
   * @returns Promise<列表结果>
   *
   * @example
   * ```typescript
   * const result = await storage.list('documents/', {
   *   limit: 50,
   *   sortBy: 'uploadedAt',
   *   sortOrder: 'desc'
   * });
   * ```
   */
  list(
    prefix: string,
    options?: ListOptions
  ): Promise<ListResult>;

  /**
   * 复制文件
   *
   * @param sourceId 源文件 ID
   * @param options 复制选项 (可选)
   * @returns Promise<新文件 ID>
   * @throws {NotFoundError} 源文件不存在
   *
   * @example
   * ```typescript
   * const newFileId = await storage.copy('source-uuid', {
   *   targetCategory: 'backups',
   *   preserveMetadata: true
   * });
   * ```
   */
  copy(
    sourceId: string,
    options?: CopyOptions
  ): Promise<string>;

  /**
   * 移动文件 (重命名/移动到新分类)
   *
   * @param fileId 文件 ID
   * @param newCategory 新分类
   * @param newMetadata 新元数据 (可选)
   * @returns Promise<是否成功>
   */
  move(
    fileId: string,
    newCategory: FileCategory,
    newMetadata?: Partial<FileMetadata>
  ): Promise<boolean>;

  /**
   * 检查文件是否存在
   *
   * @param fileId 文件 ID
   * @returns Promise<是否存在>
   *
   * @example
   * ```typescript
   * if (await storage.exists('file-uuid')) {
   *   // 文件存在
   * }
   * ```
   */
  exists(fileId: string): Promise<boolean>;

  /**
   * 获取文件元数据
   *
   * @param fileId 文件 ID
   * @returns Promise<文件信息>
   * @throws {NotFoundError} 文件不存在
   *
   * @example
   * ```typescript
   * const fileInfo = await storage.getMetadata('file-uuid');
   * console.log(fileInfo.size, fileInfo.mimeType);
   * ```
   */
  getMetadata(fileId: string): Promise<FileInfo>;

  /**
   * 更新文件元数据
   *
   * @param fileId 文件 ID
   * @param metadata 新元数据 (部分更新)
   * @returns Promise<是否成功>
   * @throws {NotFoundError} 文件不存在
   *
   * @example
   * ```typescript
   * await storage.updateMetadata('file-uuid', {
   *   tags: ['important', 'contract'],
   *   customMetadata: { version: '2.0' }
   * });
   * ```
   */
  updateMetadata(
    fileId: string,
    metadata: Partial<FileMetadata>
  ): Promise<boolean>;

  /**
   * 获取文件访问统计
   *
   * @param fileId 文件 ID
   * @returns Promise<访问统计>
   */
  getAccessStats(fileId: string): Promise<{
    downloadCount: number;
    lastAccessedAt?: Date;
    lastAccessedBy?: number;
  }>;

  /**
   * 验证文件完整性 (Checksum)
   *
   * @param fileId 文件 ID
   * @returns Promise<验证结果>
   */
  verifyIntegrity(fileId: string): Promise<{
    valid: boolean;
    checksum: string;
    algorithm: 'md5' | 'sha256';
  }>;
}

// =====================================================================
// 存储适配器基类
// =====================================================================

/**
 * 存储适配器抽象基类
 *
 * 所有具体适配器应继承此类并实现所有抽象方法
 */
export abstract class BaseStorageAdapter implements IStorageService {
  protected config: StorageConfig;
  protected logger: Logger;

  constructor(config: StorageConfig, logger?: Logger) {
    this.config = config;
    this.logger = logger || console;
  }

  // 抽象方法 - 子类必须实现
  abstract upload(
    buffer: Buffer,
    metadata: FileMetadata,
    options?: UploadOptions,
    onProgress?: UploadProgressCallback
  ): Promise<string>;

  abstract download(
    fileId: string,
    options?: DownloadOptions,
    onProgress?: DownloadProgressCallback
  ): Promise<Buffer>;

  abstract getSignedUrl(
    fileId: string,
    options?: SignedUrlOptions
  ): Promise<string>;

  abstract delete(
    fileId: string,
    options?: DeleteOptions
  ): Promise<boolean>;

  abstract list(
    prefix: string,
    options?: ListOptions
  ): Promise<ListResult>;

  abstract copy(
    sourceId: string,
    options?: CopyOptions
  ): Promise<string>;

  abstract move(
    fileId: string,
    newCategory: FileCategory,
    newMetadata?: Partial<FileMetadata>
  ): Promise<boolean>;

  abstract exists(fileId: string): Promise<boolean>;

  abstract getMetadata(fileId: string): Promise<FileInfo>;

  abstract updateMetadata(
    fileId: string,
    metadata: Partial<FileMetadata>
  ): Promise<boolean>;

  // 默认实现 - 子类可选择性覆盖
  async uploadBatch(
    files: Array<{ buffer: Buffer; metadata: FileMetadata }>,
    options?: UploadOptions
  ): Promise<string[]> {
    return Promise.all(
      files.map(f => this.upload(f.buffer, f.metadata, options))
    );
  }

  async deleteBatch(
    fileIds: string[],
    options?: DeleteOptions
  ): Promise<Array<{ fileId: string; success: boolean; error?: string }>> {
    const results = await Promise.allSettled(
      fileIds.map(id => this.delete(id, options))
    );

    return results.map((result, index) => ({
      fileId: fileIds[index],
      success: result.status === 'fulfilled',
      error: result.status === 'rejected' ? result.reason.message : undefined
    }));
  }

  async getAccessStats(fileId: string): Promise<{
    downloadCount: number;
    lastAccessedAt?: Date;
    lastAccessedBy?: number;
  }> {
    // 默认实现: 从数据库查询
    // 子类可以覆盖此方法以提供更优实现
    throw new Error('Not implemented');
  }

  async verifyIntegrity(fileId: string): Promise<{
    valid: boolean;
    checksum: string;
    algorithm: 'md5' | 'sha256';
  }> {
    // 默认实现: 下载文件并计算校验和
    throw new Error('Not implemented');
  }

  // 辅助方法
  protected validateMetadata(metadata: FileMetadata): void {
    if (!metadata.originalName) {
      throw new ValidationError('originalName is required');
    }

    if (!metadata.category) {
      throw new ValidationError('category is required');
    }

    // 防止路径遍历
    if (metadata.originalName.includes('..') || metadata.originalName.includes('/')) {
      throw new ValidationError('Invalid filename');
    }
  }

  protected generateFileId(): string {
    // 默认使用 UUID v4
    return require('uuid').v4();
  }

  protected buildStoragePath(category: FileCategory, fileId: string, ext: string): string {
    const date = new Date();
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');

    return `${category}/${year}/${month}/${fileId}${ext}`;
  }
}

// =====================================================================
// 配置接口
// =====================================================================

/**
 * 存储配置
 */
export interface StorageConfig {
  /** 存储后端类型 */
  backend: StorageBackend;

  /** 本地存储配置 */
  local?: {
    basePath: string;
  };

  /** S3 配置 */
  s3?: {
    endpoint?: string;
    region: string;
    bucket: string;
    accessKeyId: string;
    secretAccessKey: string;
    s3ForcePathStyle?: boolean;
  };

  /** MinIO 配置 */
  minio?: {
    endpoint: string;
    port: number;
    useSSL: boolean;
    bucket: string;
    accessKey: string;
    secretKey: string;
  };

  /** 阿里云 OSS 配置 */
  oss?: {
    region: string;
    bucket: string;
    accessKeyId: string;
    accessKeySecret: string;
  };

  /** 腾讯云 COS 配置 */
  cos?: {
    region: string;
    bucket: string;
    secretId: string;
    secretKey: string;
  };

  /** 通用配置 */
  maxFileSize?: number;            // 最大文件大小 (字节)
  allowedMimeTypes?: string[];     // 允许的 MIME 类型
  enableVirusScan?: boolean;       // 是否启用病毒扫描
  enableEncryption?: boolean;      // 是否启用加密
  enableVersioning?: boolean;      // 是否启用版本控制
  retentionDays?: number;          // 保留天数 (软删除)
}

/**
 * 日志接口
 */
export interface Logger {
  log(message: string, ...args: any[]): void;
  error(message: string, ...args: any[]): void;
  warn(message: string, ...args: any[]): void;
  info(message: string, ...args: any[]): void;
  debug(message: string, ...args: any[]): void;
}

// =====================================================================
// 异常类
// =====================================================================

/**
 * 存储服务基础异常
 */
export class StorageError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'StorageError';
  }
}

/**
 * 验证错误
 */
export class ValidationError extends StorageError {
  constructor(message: string) {
    super(message);
    this.name = 'ValidationError';
  }
}

/**
 * 文件未找到错误
 */
export class NotFoundError extends StorageError {
  constructor(fileId: string) {
    super(`File not found: ${fileId}`);
    this.name = 'NotFoundError';
  }
}

/**
 * 病毒扫描错误
 */
export class VirusScanError extends StorageError {
  public threat: string;

  constructor(threat: string) {
    super(`Virus detected: ${threat}`);
    this.name = 'VirusScanError';
    this.threat = threat;
  }
}

/**
 * 权限错误
 */
export class PermissionError extends StorageError {
  constructor(message: string) {
    super(message);
    this.name = 'PermissionError';
  }
}

/**
 * 配额超限错误
 */
export class QuotaExceededError extends StorageError {
  constructor(message: string) {
    super(message);
    this.name = 'QuotaExceededError';
  }
}

// =====================================================================
// 工具函数
// =====================================================================

/**
 * 从文件名提取扩展名
 */
export function getFileExtension(filename: string): string {
  const match = filename.match(/\.([^.]+)$/);
  return match ? match[1].toLowerCase() : '';
}

/**
 * 从 MIME 类型推断扩展名
 */
export function getExtensionFromMimeType(mimeType: string): string {
  const mimeMap: Record<string, string> = {
    'application/pdf': 'pdf',
    'application/msword': 'doc',
    'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'docx',
    'application/vnd.ms-excel': 'xls',
    'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'xlsx',
    'image/jpeg': 'jpg',
    'image/png': 'png',
    'image/gif': 'gif',
    'text/plain': 'txt',
    'application/zip': 'zip',
    'application/json': 'json'
  };

  return mimeMap[mimeType] || 'bin';
}

/**
 * 格式化文件大小
 */
export function formatFileSize(bytes: number): string {
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let size = bytes;
  let unitIndex = 0;

  while (size >= 1024 && unitIndex < units.length - 1) {
    size /= 1024;
    unitIndex++;
  }

  return `${size.toFixed(2)} ${units[unitIndex]}`;
}

/**
 * 验证文件类型
 */
export function isValidMimeType(mimeType: string, allowedTypes: string[]): boolean {
  return allowedTypes.length === 0 || allowedTypes.includes(mimeType);
}

/**
 * 生成安全的文件名
 */
export function sanitizeFilename(filename: string): string {
  return filename
    .replace(/[^a-zA-Z0-9._-]/g, '_')  // 替换特殊字符
    .replace(/_{2,}/g, '_')             // 去重下划线
    .substring(0, 255);                  // 限制长度
}

// =====================================================================
// 导出
// =====================================================================

export default IStorageService;
