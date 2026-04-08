// UniCheck — app.js
// JS Interop helpers cho Blazor Server

// Toggle sidebar
function toggleSidebar() {
    document.querySelector('.app-sidebar')?.classList.toggle('collapsed');
    document.querySelector('.app-content')?.classList.toggle('sidebar-collapsed');
}

// Toggle user dropdown (close others first)
function toggleUserMenu(el) {
    const drop = el.querySelector('.user-dropdown');
    const isOpen = drop.classList.contains('open');
    document.querySelectorAll('.user-dropdown.open').forEach(d => d.classList.remove('open'));
    if (!isOpen) drop.classList.add('open');
}

// Close user menu when clicking outside
document.addEventListener('click', function (e) {
    if (!e.target.closest('.user-menu')) {
        document.querySelectorAll('.user-dropdown.open').forEach(d => d.classList.remove('open'));
    }
});

// Toggle password visibility
function togglePassword() {
    const input = document.getElementById('passwordInput');
    const icon = document.getElementById('eyeIcon');
    if (!input) return;
    const isPass = input.type === 'password';
    input.type = isPass ? 'text' : 'password';
    if (icon) icon.className = isPass ? 'fa fa-eye-slash' : 'fa fa-eye';
}

// renderQrCode: QR image giờ được render phía server bởi QrCodeService (base64 PNG)
// Không cần JS interop nữa.

// Trigger browser file download từ base64 string (dùng cho ExportExcel)
function downloadFromBase64(base64, fileName, mimeType) {
    const byteChars = atob(base64);
    const byteNums = new Array(byteChars.length);
    for (let i = 0; i < byteChars.length; i++) byteNums[i] = byteChars.charCodeAt(i);
    const blob = new Blob([new Uint8Array(byteNums)], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

// Show toast notification
function showToast(message, type = 'success') {
    const container = document.getElementById('toast-container');
    if (!container) return;
    const id = 'toast-' + Date.now();
    const colors = { success: '#10b981', danger: '#ef4444', warning: '#f59e0b', info: '#3b82f6' };
    const icons = { success: 'check-circle', danger: 'times-circle', warning: 'exclamation-triangle', info: 'info-circle' };
    container.insertAdjacentHTML('beforeend', `
        <div id="${id}" class="toast align-items-center show" role="alert"
             style="border-left:4px solid ${colors[type] || '#10b981'};min-width:280px">
            <div class="d-flex">
                <div class="toast-body d-flex align-items-center gap-2">
                    <i class="fa fa-${icons[type] || 'info-circle'}" style="color:${colors[type]}"></i>
                    ${message}
                </div>
                <button type="button" class="btn-close me-2 m-auto"
                        onclick="document.getElementById('${id}').remove()"></button>
            </div>
        </div>
    `);
    setTimeout(() => document.getElementById(id)?.remove(), 4000);
}
// Show error dialog
function showErrorDialog(message) {
    console.log("Showing error dialog:", message);
    const modalHtml = `

        <div id="error-modal-overlay" class="modal-overlay" onclick="closeErrorDialog()">
            <div class="modal-dialog animate__animated animate__bounceIn" onclick="event.stopPropagation()">
                <div class="modal-header-err">
                    <i class="fa fa-circle-xmark modal-icon-err"></i>
                </div>
                <div class="modal-body-err">
                    <h3>Đăng nhập thất bại</h3>
                    <p>${message}</p>
                </div>
                <div class="modal-footer-err">
                    <button class="btn-err-close" onclick="closeErrorDialog()">Đóng</button>
                </div>
            </div>
        </div>
    `;
    document.body.insertAdjacentHTML('beforeend', modalHtml);
}

function closeErrorDialog() {
    const modal = document.getElementById('error-modal-overlay');
    if (modal) {
        modal.classList.add('animate__fadeOut');
        setTimeout(() => modal.remove(), 300);
    }
}

function submitHiddenForm(username, password) {
    console.log("Submitting hidden form for:", username);
    const form = document.getElementById('hidden-login-form');
    if (form) {
        form.querySelector('input[name="Username"]').value = username;
        form.querySelector('input[name="Password"]').value = password;
        form.submit();
    }
}

