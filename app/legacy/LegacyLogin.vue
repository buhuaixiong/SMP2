<template>
  <!-- 應用程式根容器 -->
  <div class="bg-gray-50 dark:bg-gray-900 min-h-screen">
    
    <!-- 頁面容器：根據 currentPage 的值，使用 v-if 動態顯示不同的頁面元件 -->
    
    <!-- 1. 登入頁面 -->
    <div v-if="currentPage === 'login'" id="login-page" class="min-h-screen login-bg flex items-center justify-center px-4 fade-in">
      <div class="bg-white dark:bg-gray-800 rounded-lg shadow-2xl p-8 w-full max-w-md">
        <div class="text-center mb-8">
          <i class="fas fa-building text-4xl text-[#5D5CDE] mb-4"></i>
          <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
            供應商管理系統
          </h1>
          <p class="text-gray-600 dark:text-gray-400 mt-2">
            統一登入頁面
          </p>
        </div>
        
        <!-- 登入表單 -->
        <!-- @submit.prevent 用於攔截表單的預設提交行為，改為執行 handleLogin 方法 -->
        <form @submit.prevent="handleLogin" class="space-y-6">
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">帳號</label>
            <!-- v-model 實現了表單輸入和 loginForm.username 數據的雙向綁定 -->
            <input type="text" v-model="loginForm.username" class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-[#5D5CDE] focus:border-transparent dark:bg-gray-700 dark:text-white" placeholder="請輸入帳號" required>
          </div>
          
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">密碼</label>
            <input type="password" v-model="loginForm.password" class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-[#5D5CDE] focus:border-transparent dark:bg-gray-700 dark:text-white" placeholder="請輸入密碼" required>
          </div>
          
          <div>
            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">身份</label>
            <select v-model="loginForm.role" class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-[#5D5CDE] focus:border-transparent dark:bg-gray-700 dark:text-white">
              <option value="supplier">供應商</option>
              <option value="internal">採購/品質</option>
              <option value="admin">系統管理員</option>
            </select>
          </div>
          
          <button type="submit" class="w-full bg-[#5D5CDE] text-white py-3 rounded-lg hover:bg-[#4845c7] transition-colors font-medium">
            登入
          </button>
        </form>
        
        <div class="mt-6 space-y-4">
          <div class="text-center">
            <!-- @click 監聽點擊事件，觸發 changePage 方法切換到申請頁面 -->
            <button @click="changePage('application')" class="w-full bg-green-600 text-white py-3 rounded-lg hover:bg-green-700 transition-colors font-medium">
              申請成為供應商
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- 2. 供應商申請頁面 -->
    <div v-else-if="currentPage === 'application'" id="supplier-application-page" class="min-h-screen bg-gray-50 dark:bg-gray-900 py-8 px-4 fade-in">
        <div class="max-w-4xl mx-auto">
            <div class="bg-white dark:bg-gray-800 rounded-lg shadow-2xl p-8">
                <div class="text-center mb-8">
                    <i class="fas fa-building text-4xl text-green-600 mb-4"></i>
                    <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
                        供應商資質申請
                    </h1>
                    <p class="text-gray-600 dark:text-gray-400 mt-2">
                        請填寫完整的公司資訊以申請成為我們的供應商
                    </p>
                </div>
                
                <form @submit.prevent="handleApplicationSubmit" class="space-y-6">
                    <!-- 表單內容與原 HTML 相同，此處省略以保持簡潔 -->
                    <div class="grid md:grid-cols-2 gap-6">
                        <div>
                            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">公司名稱 *</label>
                            <input type="text" required class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-gray-700 dark:text-white">
                        </div>
                        <div>
                            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">公司規模</label>
                            <select class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-gray-700 dark:text-white">
                                <option value="">請選擇</option>
                                <option value="small">小型企業 (20人以下)</option>
                                <option value="medium">中型企業 (20-300人)</option>
                                <option value="large">大型企業 (300人以上)</option>
                            </select>
                        </div>
                        <div>
                            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">法人姓名 *</label>
                            <input type="text" required class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-gray-700 dark:text-white">
                        </div>
                         <div>
                            <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">聯繫人 *</label>
                            <input type="text" required class="w-full text-base px-4 py-3 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-green-500 dark:bg-gray-700 dark:text-white">
                        </div>
                    </div>
                    
                    <div class="flex justify-end space-x-4 pt-6">
                        <!-- 返回登入頁面 -->
                        <button type="button" @click="changePage('login')" class="px-6 py-3 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 rounded-lg">
                            返回登入
                        </button>
                        <button type="submit" class="px-6 py-3 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors">
                            提交申請
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>

    <!-- 3. 主系統頁面 -->
    <div v-else-if="currentPage === 'main'" id="main-system" class="fade-in">
        <!-- 頁首 -->
        <header class="bg-white dark:bg-gray-800 shadow-lg">
            <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
                <div class="flex justify-between items-center py-4">
                    <h1 class="text-2xl font-bold text-gray-900 dark:text-white">
                        供應商管理系統
                    </h1>
                    <div class="flex items-center space-x-4">
                        <div class="flex items-center space-x-2 text-gray-700 dark:text-gray-300">
                            <i class="fas fa-user-circle text-xl"></i>
                            <!-- 顯示當前登入使用者的名稱 -->
                            <span>{{ currentUser?.name }}</span>
                        </div>
                        <button @click="handleLogout" class="bg-gray-500 text-white px-4 py-2 rounded-lg hover:bg-gray-600 transition-colors">
                            退出
                        </button>
                    </div>
                </div>
            </div>
        </header>
    
        <!-- 主內容區域 -->
        <main class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
            <!-- 導航分頁 -->
            <div class="mb-8">
                <nav class="flex space-x-8 border-b border-gray-200 dark:border-gray-700">
                    <!-- 使用 :class 動態綁定 class，判斷當前分頁是否為 active -->
                    <button @click="activeTab = 'profile'" :class="['tab-btn pb-4 px-1 font-medium', { 'active': activeTab === 'profile' }]">
                        資料管理
                    </button>
                    <button @click="activeTab = 'approval'" :class="['tab-btn pb-4 px-1 font-medium', { 'active': activeTab === 'approval' }]">
                        審批狀態
                    </button>
                    <button @click="activeTab = 'history'" :class="['tab-btn pb-4 px-1 font-medium', { 'active': activeTab === 'history' }]">
                        審批歷史
                    </button>
                    <!-- 新增：對賬分頁按鈕 -->
                    <button @click="activeTab = 'reconciliation'" :class="['tab-btn pb-4 px-1 font-medium', { 'active': activeTab === 'reconciliation' }]">
                        對賬
                    </button>
                </nav>
            </div>

            <!-- 分頁內容 -->
            <!-- v-show 根據條件切換元素的顯示，它會保留 DOM 元素，只切換 display 屬性 -->
            <div v-show="activeTab === 'profile'" class="tab-content fade-in">
                <!-- 資料管理內容，此處省略以保持簡潔 -->
                <div class="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-6 mb-8">
                    <h2 class="text-xl font-semibold text-gray-900 dark:text-white">基本資訊</h2>
                </div>
            </div>
            <div v-show="activeTab === 'approval'" class="tab-content fade-in">
                 <!-- 審批狀態內容，此處省略以保持簡潔 -->
                <div class="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-6">
                    <h2 class="text-xl font-semibold text-gray-900 dark:text-white">審批流程狀態</h2>
                </div>
            </div>
            <div v-show="activeTab === 'history'" class="tab-content fade-in">
                <!-- 審批歷史內容，此處省略以保持簡潔 -->
                <div class="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-6">
                    <h2 class="text-xl font-semibold text-gray-900 dark:text-white">審批歷史</h2>
                </div>
            </div>
            <!-- 新增：對賬分頁內容 -->
            <div v-show="activeTab === 'reconciliation'" class="tab-content fade-in">
                <div class="grid lg:grid-cols-2 gap-8">
                    <!-- 數據導出區塊 -->
                    <div class="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-6">
                        <h2 class="text-xl font-semibold text-gray-900 dark:text-white mb-6">導出入庫數據</h2>
                        <p class="text-gray-600 dark:text-gray-400 mb-4">點擊下方按鈕導出指定時間範圍內的入庫數據 Excel 報表。</p>
                        <button @click="handleExportData" class="bg-green-600 text-white px-6 py-3 rounded-lg hover:bg-green-700 transition-colors">
                            <i class="fas fa-file-excel mr-2"></i>導出 Excel
                        </button>
                    </div>

                    <!-- 發票上傳區塊 -->
                    <div class="bg-white dark:bg-gray-800 rounded-lg shadow-lg p-6">
                        <h2 class="text-xl font-semibold text-gray-900 dark:text-white mb-6">上傳發票</h2>
                        <div class="border-2 border-dashed border-gray-300 dark:border-gray-600 rounded-lg p-6 text-center mb-4">
                            <i class="fas fa-cloud-upload-alt text-4xl text-gray-400 mb-4"></i>
                            <p class="text-gray-600 dark:text-gray-400 mb-2">點擊或拖拽發票文件到此處</p>
                            <input type="file" id="invoice-upload" @change="handleInvoiceUpload" multiple class="hidden">
                            <button @click="triggerInvoiceUpload" class="bg-[#5D5CDE] text-white px-4 py-2 rounded-lg hover:bg-[#4845c7] transition-colors">
                                選擇文件
                            </button>
                        </div>
                        
                        <!-- 已上傳發票列表 -->
                        <div id="uploaded-invoices" class="space-y-2 mb-4 max-h-48 overflow-y-auto">
                            <!-- 使用 v-for 遍歷 invoiceFiles 陣列來顯示已選擇的文件 -->
                            <div v-for="(file, index) in invoiceFiles" :key="index" class="flex items-center justify-between p-3 bg-gray-50 dark:bg-gray-700 rounded-lg">
                                <div class="flex items-center space-x-3 overflow-hidden">
                                    <i class="fas fa-file-invoice-dollar text-gray-400"></i>
                                    <span class="text-sm text-gray-700 dark:text-gray-300 truncate" :title="file.name">{{ file.name }}</span>
                                    <span class="text-xs text-gray-500 dark:text-gray-400 flex-shrink-0">{{ (file.size / 1024).toFixed(1) }} KB</span>
                                </div>
                                <button @click="removeInvoiceFile(index)" class="text-red-500 hover:text-red-700 ml-2">
                                    <i class="fas fa-times"></i>
                                </button>
                            </div>
                            <p v-if="invoiceFiles.length === 0" class="text-sm text-center text-gray-500 dark:text-gray-400 py-4">暫無上傳的發票</p>
                        </div>

                        <div class="flex justify-end">
                            <!-- 使用 :disabled 動態綁定 disabled 屬性，當沒有文件時禁用按鈕 -->
                            <button @click="handleSubmitInvoices" class="bg-blue-600 text-white px-6 py-3 rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed" :disabled="invoiceFiles.length === 0">
                                <i class="fas fa-paper-plane mr-2"></i>提交發票
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </main>
    </div>

    <!-- 全域提示框/Modal -->
    <!-- 使用 v-if 條件渲染，只有當 alert.show 為 true 時才顯示 -->
    <div v-if="alert.show" class="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50">
        <div class="bg-white dark:bg-gray-800 p-6 rounded-lg shadow-lg max-w-sm w-full mx-4">
            <h3 class="text-lg font-semibold text-gray-900 dark:text-white mb-4">提示</h3>
            <p class="text-gray-600 dark:text-gray-400 mb-6">{{ alert.message }}</p>
            <div class="flex justify-end">
                <button @click="alert.show = false" class="px-4 py-2 bg-[#5D5CDE] text-white hover:bg-[#4845c7] rounded">
                    確定
                </button>
            </div>
        </div>
    </div>
  </div>
</template>

<script setup>
// 從 'vue' 引入 ref 和 reactive，用於創建響應式數據
import { ref, reactive } from 'vue';

// ref 用於創建基礎類型的響應式變數 (String, Number, Boolean)
// 當變數值改變時，Vue 會自動更新相關的 DOM
const currentPage = ref('login'); // 控制當前顯示的頁面，預設為 'login'
const activeTab = ref('profile'); // 控制主系統中的活動分頁，預設為 'profile'
const currentUser = ref(null); // 存儲當前登入的用戶資訊

// reactive 用於創建物件類型的響應式數據
// 它可以讓物件內的所有屬性都變成響應式的
const loginForm = reactive({
  username: '',
  password: '',
  role: 'supplier'
});

const alert = reactive({
  show: false,
  message: ''
});

// 新增：用於存放待上傳發票文件的響應式陣列
const invoiceFiles = reactive([]);

// 模擬的用戶資料庫
const users = {
    'supplier001': { password: '123456', role: 'supplier', name: '張三供應商' },
    'buyer001': { password: '123456', role: 'internal', name: '李四採購員' },
    'admin001': { password: '123456', role: 'admin', name: '王五管理員' }
};

// --- 方法 Methods ---

// 顯示提示框的方法
const showAlert = (message) => {
  alert.message = message;
  alert.show = true;
};

// 切換頁面的方法
const changePage = (pageName) => {
  currentPage.value = pageName;
};

// 處理登入邏輯的方法
const handleLogin = () => {
  const { username, password, role } = loginForm;

  if (!username || !password) {
    showAlert('請輸入帳號和密碼');
    return;
  }

  const user = users[username];

  if (!user || user.password !== password || user.role !== role) {
    showAlert('帳號、密碼或身份錯誤');
    return;
  }

  // 登入成功
  currentUser.value = user;
  showAlert('登入成功！');
  changePage('main'); // 切換到主系統頁面
};

// 處理登出邏輯的方法
const handleLogout = () => {
  currentUser.value = null;
  // 重置登入表單
  loginForm.username = '';
  loginForm.password = '';
  loginForm.role = 'supplier';
  showAlert('已成功退出');
  changePage('login'); // 切換回登入頁面
};

// 處理供應商申請提交的方法
const handleApplicationSubmit = () => {
    showAlert('申請已提交！我們將在3個工作日內審核您的申請，請耐心等待。');
    // 提交後延遲2秒返回登入頁面
    setTimeout(() => {
        changePage('login');
    }, 2000);
};

// --- 新增：對賬功能相關方法 ---

// 觸發隱藏的 file input 點擊事件
const triggerInvoiceUpload = () => {
  document.getElementById('invoice-upload').click();
};

// 處理發票文件選擇後的邏輯
const handleInvoiceUpload = (event) => {
  // 從事件中獲取選擇的文件列表，並轉換為真實陣列
  const files = Array.from(event.target.files);
  // 將新選擇的文件添加到 invoiceFiles 陣列中
  files.forEach(file => invoiceFiles.push(file));
  // 清空 file input 的值，以確保下次選擇相同文件時仍能觸發 change 事件
  event.target.value = '';
};
    
// 移除指定索引的已選擇發票文件
const removeInvoiceFile = (index) => {
  invoiceFiles.splice(index, 1);
};

// 處理導出數據的按鈕點擊事件 (模擬)
const handleExportData = () => {
  showAlert('正在導出數據... (模擬操作)');
  // 在實際應用中，這裡會是調用後端 API 來生成並下載文件的邏輯
};

// 處理提交發票的按鈕點擊事件 (模擬)
const handleSubmitInvoices = () => {
  if (invoiceFiles.length === 0) {
    showAlert('請先選擇要上傳的發票文件。');
    return;
  }
  showAlert(`成功提交 ${invoiceFiles.length} 個發票文件！(模擬操作)`);
  // 在實際應用中，這裡會是將文件上傳到伺服器的邏輯
  
  // 提交成功後，清空已上傳的發票列表
  invoiceFiles.length = 0;
};

</script>

<style>
/* 將原有的 CSS 樣式直接複製到這裡 */
.fade-in {
    animation: fadeIn 0.5s ease-in-out;
}
@keyframes fadeIn {
    from { opacity: 0; transform: translateY(10px); }
    to { opacity: 1; transform: translateY(0); }
}
.login-bg {
    background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
/* 分頁按鈕樣式 */
.tab-btn {
  color: #6b7280; /* dark:text-gray-400 */
  transition: all 0.2s ease-in-out;
  white-space: nowrap;
}
.tab-btn:hover {
  color: #374151; /* dark:hover:text-gray-300 */
}
/* 當前活動分頁的樣式 */
.tab-btn.active {
  color: #5D5CDE;
  border-bottom: 2px solid #5D5CDE;
}
</style>

