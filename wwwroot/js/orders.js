document.addEventListener("DOMContentLoaded", function () {
    // Add effects for collapsible details
    const detailsButtons = document.querySelectorAll(".btn-details");
    detailsButtons.forEach(button => {
        button.addEventListener("click", function () {
            const targetId = this.getAttribute("data-bs-target");
            const targetElement = document.querySelector(targetId);
            targetElement.classList.toggle("show");
        });
    });
});