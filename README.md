# Project Working Rules

Doc nay la checklist can doc truoc khi lam bat ky task nao trong project.

## 1. Think Before Coding

- Khong gia dinh am tham. Neu chua ro, noi thang dieu dang thieu.
- Neu co nhieu cach hieu yeu cau, neu ra cac cach hieu truoc khi lam.
- Neu co cach don gian hon, noi ro va uu tien cach don gian.
- Neu yeu cau co rui ro lam lech nghiep vu, dung lai va hoi.

## 2. Simplicity First

- Chi lam dung phan duoc yeu cau.
- Khong them tinh nang du phong neu chua can.
- Khong tao abstraction cho logic chi dung mot lan.
- Khong them cau hinh, tuy bien, hoac error handling khong can thiet.
- Neu thay giai phap dang qua dai, rut gon truoc khi tiep tuc.

## 3. Surgical Changes

- Chi sua file va dong code lien quan truc tiep den task.
- Khong refactor code lan can neu khong duoc yeu cau.
- Giu style hien co cua project.
- Chi don import, bien, ham ma thay doi cua minh lam du thua.
- Neu thay dead code khong lien quan, chi bao lai, khong tu xoa.

## 4. Goal-Driven Execution

Voi task nhieu buoc, truoc khi code can neu ngan gon:

1. Buoc can lam -> cach verify.
2. Buoc can lam -> cach verify.
3. Buoc can lam -> cach verify.

Thanh cong phai co tieu chi kiem chung ro rang, vi du:

- Build pass.
- Test lien quan pass.
- UI duoc kiem tra bang browser neu co thay doi frontend.
- Logic nghiep vu dung theo yeu cau.

## 5. Server Handoff Rule

- Khi can chay dev server de verify, duoc phep chay tam thoi.
- Truoc khi ban giao ket qua cuoi cung, phai dung server.
- Neu dung port mac dinh cua project, kiem tra `5097` khong con listener truoc khi tra loi.

