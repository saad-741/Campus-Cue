document.querySelectorAll("[data-demo-form]").forEach((form) => {
  form.addEventListener("submit", (event) => {
    event.preventDefault();
    const message = form.querySelector("[data-demo-message]");
    if (message) message.classList.remove("d-none");
  });
});

document.querySelectorAll("[data-demo-action]").forEach((button) => {
  button.addEventListener("click", () => {
    const original = button.textContent.trim();
    button.textContent = button.dataset.doneLabel || "Done";
    button.disabled = true;
    window.setTimeout(() => { button.textContent = original; button.disabled = false; }, 1600);
  });
});
