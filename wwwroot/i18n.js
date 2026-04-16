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
        'btn.settings': 'Настройки',
        'btn.wallet': 'Баланс аккаунта',
        'btn.close': 'Закрыть',
        'btn.save': 'Сохранить',
        'btn.cancel': 'Отмена',
        'btn.add': 'Добавить',
        'btn.remove': 'Удалить',
        'btn.export': 'Экспортировать в Excel',
        'btn.refresh': 'Обновить',
        'btn.create': 'Создать',
        'btn.next': 'Далее',
        'btn.login': 'Войти',

        // Тултипы тулбара
        'toolbar.addAccount': 'Добавить аккаунт',
        'toolbar.settings': 'Настройки',
        'toolbar.createGroup': 'Создать группу',
        'toolbar.confirmations': 'Подтверждения',
        'toolbar.trades': 'Торговые предложения',
        'toolbar.market': 'Маркет',

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
        'modal.account.autoTrade': 'Автоматическое принятие трейдов',
        'modal.account.autoMarket': 'Автоматическое принятие маркета',
        'modal.account.refreshSession': 'Обновить сессию',

        'modal.confirmations.title': 'Подтверждения',
        'modal.confirmations.noConfirmations': 'Нет подтверждений',
        'modal.confirmations.acceptAll': 'Принять все',
        'modal.confirmations.cancelAll': 'Отклонить все',

        'modal.trades.title': 'Торговые предложения',
        'modal.trades.noTrades': 'Нет торговых предложений',

        'modal.market.title': 'Маркет',
        'modal.market.noListings': 'Нет активных листингов',

        'modal.addAccount.title': 'Добавить аккаунт',
        'modal.addAccount.username': 'Логин Steam',
        'modal.addAccount.password': 'Пароль',
        'modal.addAccount.emailCode': 'Код с почты',
        'modal.addAccount.group': 'Группа',
        'modal.addAccount.step1': 'Шаг 1: Авторизация',
        'modal.addAccount.step2': 'Шаг 2: Код с почты',
        'modal.addAccount.step3': 'Шаг 3: Код Steam Guard',
        'modal.addAccount.step4': 'Шаг 4: Код восстановления',

        'modal.password.title': 'Требуется пароль',
        'modal.password.message': 'Введите пароль для обновления сессии',
        'modal.password.label': 'Пароль',

        'modal.createGroup.title': 'Создать группу',
        'modal.createGroup.name': 'Название группы',
        'modal.createGroup.placeholder': 'Введите название группы',

        'modal.addProxy.title': 'Добавить прокси',
        'modal.addProxy.name': 'Название',
        'modal.addProxy.namePlaceholder': 'Мой прокси',
        'modal.addProxy.address': 'Адрес (host:port)',
        'modal.addProxy.addressPlaceholder': '127.0.0.1:8080',
        'modal.addProxy.username': 'Логин (необязательно)',
        'modal.addProxy.usernamePlaceholder': 'user',
        'modal.addProxy.password': 'Пароль (необязательно)',
        'modal.addProxy.passwordPlaceholder': 'password',

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
        'btn.settings': 'Settings',
        'btn.wallet': 'Account balance',
        'btn.close': 'Close',
        'btn.save': 'Save',
        'btn.cancel': 'Cancel',
        'btn.add': 'Add',
        'btn.remove': 'Remove',
        'btn.export': 'Export to Excel',
        'btn.refresh': 'Refresh',
        'btn.create': 'Create',
        'btn.next': 'Next',
        'btn.login': 'Login',

        // Toolbar tooltips
        'toolbar.addAccount': 'Add Account',
        'toolbar.settings': 'Settings',
        'toolbar.createGroup': 'Create Group',
        'toolbar.confirmations': 'Confirmations',
        'toolbar.trades': 'Trade Offers',
        'toolbar.market': 'Market',

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
        'modal.account.autoTrade': 'Auto-accept trades',
        'modal.account.autoMarket': 'Auto-accept market',
        'modal.account.refreshSession': 'Refresh session',

        'modal.confirmations.title': 'Confirmations',
        'modal.confirmations.noConfirmations': 'No confirmations',
        'modal.confirmations.acceptAll': 'Accept All',
        'modal.confirmations.cancelAll': 'Cancel All',

        'modal.trades.title': 'Trade Offers',
        'modal.trades.noTrades': 'No trade offers',

        'modal.market.title': 'Market',
        'modal.market.noListings': 'No active listings',

        'modal.addAccount.title': 'Add Account',
        'modal.addAccount.username': 'Steam Login',
        'modal.addAccount.password': 'Password',
        'modal.addAccount.emailCode': 'Email Code',
        'modal.addAccount.group': 'Group',
        'modal.addAccount.step1': 'Step 1: Authorization',
        'modal.addAccount.step2': 'Step 2: Email Code',
        'modal.addAccount.step3': 'Step 3: Steam Guard Code',
        'modal.addAccount.step4': 'Step 4: Recovery Code',

        'modal.password.title': 'Password Required',
        'modal.password.message': 'Enter password to refresh session',
        'modal.password.label': 'Password',

        'modal.createGroup.title': 'Create Group',
        'modal.createGroup.name': 'Group Name',
        'modal.createGroup.placeholder': 'Enter group name',

        'modal.addProxy.title': 'Add Proxy',
        'modal.addProxy.name': 'Name',
        'modal.addProxy.namePlaceholder': 'My proxy',
        'modal.addProxy.address': 'Address (host:port)',
        'modal.addProxy.addressPlaceholder': '127.0.0.1:8080',
        'modal.addProxy.username': 'Username (optional)',
        'modal.addProxy.usernamePlaceholder': 'user',
        'modal.addProxy.password': 'Password (optional)',
        'modal.addProxy.passwordPlaceholder': 'password',

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

        if (el.tagName === 'INPUT' && (el.type === 'text' || el.type === 'password')) {
            el.placeholder = translation;
        } else {
            el.textContent = translation;
        }
    });

    // Обновляем placeholder'ы с data-i18n-placeholder атрибутом
    document.querySelectorAll('[data-i18n-placeholder]').forEach(el => {
        const key = el.getAttribute('data-i18n-placeholder');
        const translation = t(key);
        el.placeholder = translation;
    });

    // Обновляем тултипы тулбара
    updateToolbarTooltips();
}

// Функция для обновления тултипов тулбара
function updateToolbarTooltips() {
    // Эта функция будет вызвана из index.html после инициализации тултипов
    if (typeof setupTooltip !== 'undefined') {
        document.querySelectorAll('.toolbar-btn-add').forEach(btn => {
            btn.setAttribute('data-tooltip-key', 'toolbar.addAccount');
        });
        document.querySelectorAll('.toolbar-btn-settings').forEach(btn => {
            btn.setAttribute('data-tooltip-key', 'toolbar.settings');
        });
        document.querySelectorAll('.toolbar-btn-create-group').forEach(btn => {
            btn.setAttribute('data-tooltip-key', 'toolbar.createGroup');
        });
        document.querySelectorAll('.toolbar-btn-confirmations').forEach(btn => {
            btn.setAttribute('data-tooltip-key', 'toolbar.confirmations');
        });
        document.querySelectorAll('.toolbar-btn-trades').forEach(btn => {
            btn.setAttribute('data-tooltip-key', 'toolbar.trades');
        });
        document.querySelectorAll('.toolbar-btn-market').forEach(btn => {
            btn.setAttribute('data-tooltip-key', 'toolbar.market');
        });
    }
}

// Инициализация языка при загрузке
function initLanguage(lang) {
    // Если язык передан из C# - используем его, иначе берем из localStorage или ru по умолчанию
    const language = lang || localStorage.getItem('language') || 'ru';
    setLanguage(language);
}
