
const tg = window.Telegram.WebApp;
console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);
document.addEventListener("DOMContentLoaded", () => {
	tg.ready();
    if (tg && tg.BackButton) {
        tg.BackButton.show(); 
        tg.BackButton.onClick(goBack); 
    }

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
    if (tg && tg.close) {
        tg.close(); 
    } else {
        window.history.back();
    }
}
