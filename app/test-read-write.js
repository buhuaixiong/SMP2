const ExcelJS = require('exceljs');
const path = require('path');

(async () => {
  const templatePath = path.join('template/InquiryIndirectmaterialPR.xlsm');
  const workbook = new ExcelJS.Workbook();
  await workbook.xlsx.readFile(templatePath);
  console.log('worksheets', workbook.worksheets.map(w => w.name));
  const buffer = await workbook.xlsx.writeBuffer();
  require('fs').writeFileSync('tmp.xlsx', Buffer.from(buffer));
  console.log('done');
})();
