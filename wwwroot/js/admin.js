// Toggle row visibility
document.addEventListener("DOMContentLoaded", function () {
    const toggleButtons = document.querySelectorAll(".toggle-btn");

    toggleButtons.forEach(button => {
        button.addEventListener("click", function () {
            const targetId = this.getAttribute("data-target");
            const targetRow = document.getElementById(targetId);

            if (targetRow.classList.contains("hidden-row")) {
                targetRow.classList.remove("hidden-row");
                this.textContent = "Hide Details";
            } else {
                targetRow.classList.add("hidden-row");
                this.textContent = "Show Details";
            }
        });
    });
});