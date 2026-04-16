// Локализация интерфейса
const translations = {
    ru: {
        // Главный экран
        'app.title': 'Awakened Steam Desktop Authenticator',
        'accounts.title': 'Аккаунты',
        'accounts.search': 'Поиск аккаунтов...',
        'accounts.noAccounts': 'Нет аккаунтов',
        'accounts.addAccount': 'Добавить аккаунт',

        // Кнопки действий
        'btn.copy': 'Копировать код',
        'btn.confirmations': 'Подтверждения',
        'btn.settings': 'Настройки аккаунта',
        'btn.wallet': 'Баланс аккаунта',
        'btn.close': 'Закрыть',
        'btn.save': 'Сохранить',
        'btn.cancel': 'Отмена',
        'btn.add': 'Добавить',
        'btn.remove': 'Удалить',
        'btn.export': 'Экспортировать в Excel',

        // Модальные окна
        'modal.settings.title': 'Настройки',
        'modal.settings.general': 'Общие',
        'modal.settings.language': 'Язык интерфейса',
        'modal.settings.defaultGroup': 'Стандартная группа при добавлении аккаунта',
        'modal.settings.hideLogins': 'Скрывать логины (показывать только первые и последние символы)',
        'modal.settings.export': 'Экспорт данных',
        'modal.settings.proxy': 'Прокси',
        'modal.settings.proxyName': 'Название',
        'modal.settings.proxyAddress': 'Адрес (host:port)',
        'modal.settings.proxyUsername': 'Логин',
        'modal.settings.proxyPassword': 'Пароль',
        'modal.settings.proxyActive': 'Активен',

        'modal.account.title': 'Настройки аккаунта',
        'modal.account.username': 'Имя пользователя',
        'modal.account.group': 'Группа',
        'modal.account.proxy': 'Прокси',
        'modal.account.noProxy': 'Без прокси',
        'modal.account.revocationCode': 'Код отзыва',
        'modal.account.removeAccount': 'Удалить аккаунт',

        'modal.confirmations.title': 'Подтверждения',
        'modal.confirmations.noConfirmations': 'Нет подтверждений',
        'modal.confirmations.acceptAll': 'Принять все',
        'modal.confirmations.cancelAll': 'Отклонить все',

        'modal.addAccount.title': 'Добавить аккаунт',
        'modal.addAccount.username': 'Логин Steam',
        'modal.addAccount.password': 'Пароль',
        'modal.addAccount.emailCode': 'Код с почты (если требуется)',
        'modal.addAccount.group': 'Группа',

        // Уведомления
        'toast.codeCopied': 'Код скопирован',
        'toast.saved': 'Сохранено',
        'toast.error': 'Ошибка',
        'toast.loading': 'Загрузка...',
    },
    en: {
        // Main screen
        'app.title': 'Awakened Steam Desktop Authenticator',
        'accounts.title': 'Accounts',
        'accounts.search': 'Search accounts...',
        'accounts.noAccounts': 'No accounts',
        'accounts.addAccount': 'Add Account',

        // Action buttons
        'btn.copy': 'Copy code',
        'btn.confirmations': 'Confirmations',
        'btn.settings': 'Account settings',
        'btn.wallet': 'Account balance',
        'btn.close': 'Close',
        'btn.save': 'Save',
        'btn.cancel': 'Cancel',
        'btn.add': 'Add',
        'btn.remove': 'Remove',
        'btn.export': 'Export to Excel',

        // Modals
        'modal.settings.title': 'Settings',
        'modal.settings.general': 'General',
        'modal.settings.language': 'Interface Language',
        'modal.settings.defaultGroup': 'Default Group',
        'modal.settings.hideLogins': 'Hide logins (show only first and last characters)',
        'modal.settings.export': 'Export Data',
        'modal.settings.proxy': 'Proxy',
        'modal.settings.proxyName': 'Name',
        'modal.settings.proxyAddress': 'Address (host:port)',
        'modal.settings.proxyUsername': 'Username',
        'modal.settings.proxyPassword': 'Password',
        'modal.settings.proxyActive': 'Active',

        'modal.account.title': 'Account Settings',
        'modal.account.username': 'Username',
        'modal.account.group': 'Group',
        'modal.account.proxy': 'Proxy',
        'modal.account.noProxy': 'No proxy',
        'modal.account.revocationCode': 'Revocation Code',
        'modal.account.removeAccount': 'Remove Account',

        'modal.confirmations.title': 'Confirmations',
        'modal.confirmations.noConfirmations': 'No confirmations',
        'modal.confirmations.acceptAll': 'Accept All',
        'modal.confirmations.cancelAll': 'Cancel All',

        'modal.addAccount.title': 'Add Account',
        'modal.addAccount.username': 'Steam Login',
        'modal.addAccount.password': 'Password',
        'modal.addAccount.emailCode': 'Email Code (if required)',
        'modal.addAccount.group': 'Group',

        // Notifications
        'toast.codeCopied': 'Code copied',
        'toast.saved': 'Saved',
        'toast.error': 'Error',
        'toast.loading': 'Loading...',
    }
};

// Текущий язык (по умолчанию русский)
let currentLanguage = 'ru';

// Функция перевода
function t(key) {
    return translations[currentLanguage][key] || key;
}

// Функция смены языка
function setLanguage(lang) {
    if (!translations[lang]) {
        console.error(`Language ${lang} not found`);
        return;
    }
    currentLanguage = lang;
    localStorage.setItem('language', lang);
    updateUILanguage();
}

// Обновление всех текстов в UI
function updateUILanguage() {
    // Обновляем все элементы с data-i18n атрибутом
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const translation = t(key);

        if (el.tagName === 'INPUT' && el.type === 'text') {
            el.placeholder = translation;
        } else {
            el.textContent = translation;
        }
    });
}

// Инициализация языка при загрузке
function initLanguage(lang) {
    // Если язык передан из C# - используем его, иначе берем из localStorage или ru по умолчанию
    const language = lang || localStorage.getItem('language') || 'ru';
    setLanguage(language);
}
