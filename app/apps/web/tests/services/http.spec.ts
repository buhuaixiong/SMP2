import { describe, expect, it, vi, beforeEach } from "vitest";

const httpMocks = vi.hoisted(() => {
  const requestSpy = vi.fn();
  const getSpy = vi.fn();
  const postSpy = vi.fn();
  const putSpy = vi.fn();
  const deleteSpy = vi.fn();
  const fetchSpy = vi.fn();
  return {
    requestSpy,
    getSpy,
    postSpy,
    putSpy,
    deleteSpy,
    fetchSpy,
    module: {
      default: {
        request: requestSpy,
      },
      get: getSpy,
      post: postSpy,
      put: putSpy,
      del: deleteSpy,
      apiFetch: fetchSpy,
    },
  };
});

vi.mock("@/api/http", () => httpMocks.module);

import { httpService } from "@/services/http";

describe("httpService", () => {
  const notification = {
    error: vi.fn(),
  };

  const context = {
    get: vi.fn().mockReturnValue(notification),
  };

  beforeEach(() => {
    vi.resetAllMocks();
    context.get.mockReturnValue(notification);
  });

  it("delegates to get/post helpers", async () => {
    httpMocks.getSpy.mockResolvedValue({ data: [{ id: 1 }] });
    httpMocks.postSpy.mockResolvedValue({ id: 1 });

    const service = await httpService.setup(context as any);
    await service.get("/items");
    await service.post("/items", { name: "A" });

    expect(httpMocks.getSpy).toHaveBeenCalledWith("/items", undefined);
    expect(httpMocks.postSpy).toHaveBeenCalledWith("/items", { name: "A" }, undefined);
  });

  it("shows notification when request fails", async () => {
    httpMocks.postSpy.mockRejectedValue(new Error("network-error"));
    const service = await httpService.setup(context as any);
    await expect(service.post("/fail")).rejects.toThrow("network-error");
    expect(notification.error).toHaveBeenCalled();
  });

  it("suppresses notification in silent mode", async () => {
    httpMocks.postSpy.mockRejectedValue(new Error("fail"));
    const service = await httpService.setup(context as any);
    await expect(service.post("/fail", undefined, { silent: true })).rejects.toThrow("fail");
    expect(notification.error).not.toHaveBeenCalled();
  });
});
