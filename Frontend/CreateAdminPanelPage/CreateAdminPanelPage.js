const API_BASE = "/Parser/api";

const ErrorCodes = {
    NEED_VERIFICATION_CODE: "NEED_VERIFICATION_CODE",
    NEED_TWO_FACTOR_PASSWORD: "NEED_TWO_FACTOR_PASSWORD",
    INVALID_VERIFICATION_CODE: "INVALID_VERIFICATION_CODE",
    INVALID_TWO_FACTOR_PASSWORD: "INVALID_TWO_FACTOR_PASSWORD",
    SESSION_EXPIRED: "SESSION_EXPIRED",
    PHONE_ALREADY_IN_USE: "PHONE_ALREADY_IN_USE"
};

document.addEventListener("DOMContentLoaded", () => {
  const authForm = document.querySelector(".AuthForm");
  const confirmCodeForm = document.querySelector(".ConfirmCodeForm");
  const confirmTwoFactoPasswordForm = document.querySelector(".ConfirmTwoFactoPasswordForm");
  const codeInputs = document.querySelectorAll(".ConfirmCodeInputSingle");

  const showError = (msg) => {
    const errorPopup = document.getElementById("ErrorPopup");
    if (!errorPopup) return alert(msg);
    errorPopup.textContent = msg;
    errorPopup.classList.remove("hidden");
    errorPopup.classList.add("show");
    setTimeout(() => {
      errorPopup.classList.remove("show");
      errorPopup.classList.add("hidden");
    }, 4000);
  };

  codeInputs.forEach((inp, idx) => {
    inp.addEventListener("input", () => {
      inp.value = inp.value.replace(/\D/g, "");
      if (inp.value && codeInputs[idx + 1]) {
        codeInputs[idx + 1].focus();
      }
    });

    inp.addEventListener("keydown", (e) => {
      if (e.key === "Backspace" && inp.value === "") {
        if (codeInputs[idx - 1]) {
          codeInputs[idx - 1].focus();
        }
      }
    });
  });

authForm?.addEventListener("submit", async e => {
    e.preventDefault();
    const phone = document.getElementById("Phone").value.trim();
    const submitButton = authForm.querySelector(".AuthButton"); // Получаем кнопку отправки
    
    if (!phone) {
        showError("Пожалуйста, заполните номер телефона.");
        return;
    }

    try {
        submitButton.disabled = true;
        submitButton.textContent = "Ожидание...";
        submitButton.classList.add("loading");

        const res = await fetch(`${API_BASE}/Auth/LoginAndStartParser`, {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ phone })
        });
        
        const result = await res.json();
        
        if (res.ok) {
            if (result.message.includes(ErrorCodes.NEED_VERIFICATION_CODE)) {
                authForm.style.display = "none";
                confirmCodeForm.style.display = "block";
                confirmTwoFactoPasswordForm.style.display = "none";
            } else {
                window.location.href = "/Parser/ControlPanel/ControlPanel.html";
            }
        } else {
            showError(result.message || "Ошибка при входе.");
        }
    } catch (err) {
        showError("Ошибка запроса: " + err.message);
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = "Войти";
        submitButton.classList.remove("loading");
    }
});

  
confirmCodeForm?.addEventListener("submit", async e => {
    console.log("submit event сработал");
    e.preventDefault();

    const submitButton = confirmCodeForm.querySelector(".ConfirmCodeButton");
    submitButton.disabled = true;
    submitButton.textContent = "Ожидание...";
    submitButton.classList.add("loading");
    const code = [...codeInputs].map(i => i.value).join("").trim();

    if (code.length === 0) {
        showError("Введите код.");
        return;
    }

    submitButton.disabled = true;
    console.log(`отправляю код подтверждения ${Number(code)}`);
    
    try {
        const res = await fetch(`${API_BASE}/Auth/SendVerificationCodeFromTelegram`, {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ telegramCode: Number(code) })
        });

        const result = await res.json();
        console.log(result);
      if (res.ok) {
          window.location.href = "/Parser/ControlPanel/ControlPanel.html";
      } else {
        if (result.message) {
          if (result.message.includes(ErrorCodes.NEED_TWO_FACTOR_PASSWORD)) {
            confirmCodeForm.style.display = "none";
            confirmTwoFactoPasswordForm.style.display = "block";
          }
          else if (result.message.includes(ErrorCodes.INVALID_VERIFICATION_CODE) ||
            result.message.includes("Код недействителен")) {
            showError(result.message);
          }
          else {
            showError(result.message);
          }
        }
          
            codeInputs.forEach(input => input.value = '');
            codeInputs[0].focus();
        }
    } catch (err) {
        showError("Ошибка сети: " + (err.message || "не удалось выполнить запрос"));
    } finally {
        submitButton.disabled = false;
        submitButton.textContent = "Подтвердить";
        submitButton.classList.remove("loading");
    }
});



confirmTwoFactoPasswordForm?.addEventListener("submit", async e => {
  e.preventDefault();
  const twoFactorPasswordInput = document.getElementById("TwoFactorPassword");
  const twoFactorPassword = twoFactorPasswordInput.value.trim();
  const submitButton = confirmTwoFactoPasswordForm.querySelector(".ConfirmTwoFactoPasswordButton");

  submitButton.disabled = true;
  submitButton.textContent = "Ожидание...";
  submitButton.classList.add("loading");

  if (!twoFactorPassword) {
    alert("Введите двухфакторный пароль.");
    return;
  }

  submitButton.disabled = true;

  try {
    const res = await fetch(`${API_BASE}/Auth/SendATwoFactorPassword`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ twoFactorPassword })
    });

    const result = await res.json();

    if (res.ok) {
        window.location.href = "/Parser/ControlPanel/ControlPanel.html";
    } else {
      if (result.message.includes(ErrorCodes.INVALID_TWO_FACTOR_PASSWORD)) {
        twoFactorPasswordInput.value = "";
        alert("Неверный двухфакторный пароль.");
      } else {
        alert(result.message || "Ошибка при вводе двухфакторного пароля.");
      }
    }
  } catch (err) {
    showError("Ошибка запроса: " + err.message);
  } finally {
    submitButton.disabled = false;
    submitButton.textContent = "Подтвердить";
    submitButton.classList.remove("loading");
  }
});

});



async function ResendTwoFactorCode() {
    const resendButton = document.querySelector('.ResendCodeButton');
    if (!resendButton) return;

    try {
        resendButton.disabled = true;
        resendButton.textContent = 'Отправка...';
        
        const response = await fetch(`${API_BASE}/Auth/ResendVerificationCode`, {
            method: 'POST',
            credentials: 'include',
            headers: {
                'Content-Type': 'application/json'
            }
        });

        if (!response.ok) {
            throw new Error('Network response was not ok');
        }

        const result = await response.json();

        if (result.success) {
            console.log("Новый код отправлен");
        } else {
            console.log(result.message || 'Ошибка при отправке кода');
            alert(result.message || 'Ошибка при отправке кода');
        }
    } catch (error) {
        console.error('Ошибка:', error);
        alert('Произошла ошибка при запросе нового кода');
    } finally {
        resendButton.disabled = false;
        resendButton.textContent = 'Отправить код повторно';
    }
}

