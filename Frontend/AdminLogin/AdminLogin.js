//константы и переменные

console.log(config.API_BASE); 
console.log(config.DefaultStartFileLocation);

const token = localStorage.getItem("jwtToken");

document.addEventListener("DOMContentLoaded", async () => {
    const token = localStorage.getItem("jwtToken");

    if (token && token.trim() !== "") {
        try {
            const response = await fetch(`${config.API_BASE}/AdminAuth/ValidateToken`, {
                method: 'GET',
                headers: { "Authorization": token }
            });

            if (response.ok) {
                window.location.href = `${config.DefaultStartFileLocation}/AdminPage/AdminPage.html`;
            } else {
                localStorage.removeItem("jwtToken");
            }
        } catch (error) {
            console.error("Ошибка проверки токена:", error);
            localStorage.removeItem("jwtToken");
        }
    }
});

async function Login() {
    const login = document.getElementById("LoginInput").value;
    const password = document.getElementById("PasswordInput").value;
    console.log(login + password);
    try {
        const response = await fetch(`${config.API_BASE}/AdminAuth/Login`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
           body: JSON.stringify({ login: login, password: password })
        });

        if (response.ok) {
            const token = await response.text();
            localStorage.setItem("jwtToken", token); 
            window.location.href = `${config.DefaultStartFileLocation}/AdminPage/AdminPage.html`;
        } else {
            alert("Неверный логин или пароль!");
        }
    } catch (error) {
        console.error("Ошибка при логине:", error);
    }
}