
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	 function initTelegramWebApp() {
        if (window.Telegram && window.Telegram.WebApp) {
            const tg = window.Telegram.WebApp;
            
            console.log('WebApp version:', tg.version);
            tg.ready();

            // Скрываем основные кнопки
            tg.MainButton.hide();
            if (tg.SecondaryButton) {
                tg.SecondaryButton.hide();
            }

            // ОБХОДИМ ОГРАНИЧЕНИЕ ВЕРСИИ 6.0
            try {
                // Пробуем использовать BackButton, но ловим ошибку
                if (tg.BackButton) {
                    try {
                        tg.BackButton.show();
                        tg.BackButton.onClick(function() {
                            console.log('Back button clicked - going back');
                            if (window.history.length > 1) {
                                window.history.back();
                            } else {
                                tg.close();
                            }
                        });
                        console.log('✅ BackButton used successfully');
                    } catch (backButtonError) {
                        console.warn('❌ BackButton.show() failed, using alternative');
                        useAlternativeBackButton();
                    }
                } else {
                    useAlternativeBackButton();
                }
            } catch (error) {
                console.error('Error with back button setup:', error);
                useAlternativeBackButton();
            }
            
        } else {
            setTimeout(initTelegramWebApp, 100);
        }
    }

    // АЛЬТЕРНАТИВНЫЙ СПОСОБ ДЛЯ ВЕРСИИ 6.0
    function useAlternativeBackButton() {
        console.log('Using alternative back button for v6.0');
        
        // Попробуем разные методы
        let success = false;
        
        // Метод 1: Через расширенный API (если есть)
        if (typeof window.Telegram?.WebApp?.postEvent === 'function') {
            try {
                window.Telegram.WebApp.postEvent('web_app_setup_back_button', { 
                    is_visible: true 
                });
                success = true;
                console.log('✅ Alternative method: postEvent');
            } catch (e) {}
        }
        
        // Метод 2: Через iframe communication (для web)
        if (!success && window.parent && window.parent !== window) {
            try {
                window.parent.postMessage(JSON.stringify({
                    eventType: 'web_app_setup_back_button',
                    params: { is_visible: true }
                }), '*');
                success = true;
                console.log('✅ Alternative method: postMessage');
            } catch (e) {}
        }
        
        // Метод 3: Создаем свою кнопку
        if (!success) {
            createCustomBackButton();
            console.log('✅ Alternative method: custom button');
        }
        
        // Обработчик для альтернативных методов
        tg.onEvent('back_button_pressed', function() {
            console.log('Back button pressed - going back');
            if (window.history.length > 1) {
                window.history.back();
            } else {
                tg.close();
            }
        });
    }

    // СОЗДАЕМ СВОЮ КНОПКУ
    function createCustomBackButton() {
        if (document.getElementById('custom-back-button')) return;
        
        const backBtn = document.createElement('button');
        backBtn.id = 'custom-back-button';
        backBtn.textContent = '← Назад';
        backBtn.style.cssText = `
            position: fixed;
            top: 15px;
            left: 15px;
            z-index: 9999;
            padding: 12px 18px;
            background: var(--tg-theme-button-color, #007aff);
            color: var(--tg-theme-button-text-color, white);
            border: none;
            border-radius: 10px;
            font-size: 16px;
            font-weight: 500;
            cursor: pointer;
            box-shadow: 0 2px 12px rgba(0,0,0,0.2);
        `;
        
        backBtn.onclick = function() {
            if (window.history.length > 1) {
                window.history.back();
            } else if (window.Telegram?.WebApp?.close) {
                window.Telegram.WebApp.close();
            }
        };
        
        document.body.appendChild(backBtn);
    }

    initTelegramWebApp();



    initTelegramWebApp();
	
    document.addEventListener('click', (e) => {
        if (!e.target.closest('.LoginInput') && !e.target.closest('.LoginButton')) {
            if (document.activeElement && document.activeElement.blur) {
                document.activeElement.blur();
            }
        }
    });
    const LoginForm = document.querySelector(".LoginForm");

    LoginForm?.addEventListener("submit", async e => {
        e.preventDefault();

        const body = {
            ParserId: document.getElementById("KeySession").value,
            ParserPassword: document.getElementById("ParserPassword").value,
        };
        if (!isValidGuid(body.ParserId)) {
              alert("Некорректный ключ сессии");
              return;
        }
        try {
            const res = await fetch(`${config.API_BASE}/Auth/EnterToSessionByKeyAndPassword`, {
                method: "POST",
                credentials: "include",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(body)
            });

            if (res.ok) {
                if (tg && tg.BackButton) {
                    tg.BackButton.hide();
                }
                location.href = `${config.DefaultStartFileLocation}/ControlPanel/ControlPanel.html`;
            } else {
                const errorMessage = await res.text();
                
                if(errorMessage.includes("Неверный ключ сессии или пароль")){
                    alert("Неверный ключ сессии или пароль");
                }           
            }

        } catch (err) {
            console.error(err);
            alert("Ошибка запроса: " + err.message);
        }
    });
});

function isValidGuid(guid) {
    const regex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
    return regex.test(guid);
}

function goBack() {
    if (window.history.length > 1) {
        window.history.back();
    } else {
        if (window.Telegram && window.Telegram.WebApp && window.Telegram.WebApp.close) {
            window.Telegram.WebApp.close();
        }
    }
}
