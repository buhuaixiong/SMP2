module.exports = {
  root: true,
  parserOptions: {
    ecmaVersion: "latest",
    sourceType: "module",
  },
  overrides: [
    {
      files: ["src/**/*.{js,ts,jsx,tsx,vue}", "tests/**/*.{js,ts}"],
      rules: {
        "no-restricted-imports": [
          "error",
          {
            paths: [
              {
                name: "element-plus",
                importNames: ["ElNotification", "ElMessage", "ElMessageBox"],
                message:
                  "禁止直接调用 Element Plus 通知组件，请改用 useNotification() composable。",
              },
            ],
          },
        ],
        "no-restricted-globals": [
          "error",
          { name: "ElNotification", message: "请通过 useNotification() composable 触发通知。" },
          { name: "ElMessage", message: "请通过 useNotification() composable 触发消息提示。" },
        ],
        "no-restricted-syntax": [
          "error",
          {
            selector: "CallExpression[callee.name='ElNotification']",
            message: "统一改用 useNotification() 返回的 API。",
          },
          {
            selector: "CallExpression[callee.name='ElMessage']",
            message: "统一改用 useNotification() 返回的 API。",
          },
          {
            selector: "MemberExpression[object.name='ElNotification']",
            message: "统一改用 useNotification() 返回的 API。",
          },
          {
            selector: "MemberExpression[object.name='ElMessage']",
            message: "统一改用 useNotification() 返回的 API。",
          },
        ],
      },
    },
  ],
};
