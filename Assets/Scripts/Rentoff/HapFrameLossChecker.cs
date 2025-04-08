using UnityEngine;
using DemolitionStudios.DemolitionMedia;

public class HapFrameLossChecker : MonoBehaviour
{
    public Media media; // ������ �� ��������� Media

    private int expectedFrame = 0; // ��������� ����� �����
    private int fixedUpdateCount = 0; // ������� FixedUpdate ������
    private int frameLossCount = 0; // ������� ���������� ������
    private int earlyDrops = 0; // ������� early drops
    private int lateDrops = 0; // ������� late drops

    void Start()
    {
        // ���� media �� ��������, ��������� �������� ��� �� �������� GameObject
        if (media == null)
        {
            media = GetComponent<Media>();
        }

        // ��������, ��� media ������
        if (media == null)
        {
            Debug.LogError("Media ��������� �� ������! ���������, ��� �� �������� � Inspector ��� �������� � GameObject.");
            enabled = false; // ��������� ������, ����� �������� ������
            return;
        }

        // ��������� ��������������� �����
        media.Play();

        // �������������� expectedFrame ������� �������� �����
        expectedFrame = media.VideoCurrentFrame;

        // �������� ��������� ���������� ����������� ������
        media.GetFramedropCount(out earlyDrops, out lateDrops);
    }

    void FixedUpdate()
    {
        fixedUpdateCount++;

        // ��������� ���������� ����������� ������
        int newEarlyDrops, newLateDrops;
        media.GetFramedropCount(out newEarlyDrops, out newLateDrops);
        int droppedFrames = (newEarlyDrops + newLateDrops) - (earlyDrops + lateDrops);
        earlyDrops = newEarlyDrops;
        lateDrops = newLateDrops;

        // ���������, �� �������� �� �����
        if (media.VideoCurrentFrame != expectedFrame)
        {
            // ��������� ���������� ���������� ������
            int lostFrames = media.VideoCurrentFrame - expectedFrame;
            frameLossCount += lostFrames;

            // ��������� ����������� �����
            lostFrames += droppedFrames;

            Debug.LogWarning("FixedUpdate #" + fixedUpdateCount + ": �������� " + lostFrames + " ������! �������� ���� " + expectedFrame + ", ������� ���� " + media.VideoCurrentFrame + ". Early Drops: " + earlyDrops + ", Late Drops: " + lateDrops);
        }

        // ��������� ��������� ����� �����
        expectedFrame = media.VideoCurrentFrame + 1;

        // ������� ���������� � ������� �����
        Debug.Log("FixedUpdate #" + fixedUpdateCount + ", Current Frame: " + media.VideoCurrentFrame + ". Early Drops: " + earlyDrops + ", Late Drops: " + lateDrops);
    }

    void OnDisable()
    {
        Debug.Log("HapFrameLossChecker ��������. ����� �������� ������: " + frameLossCount + ". Early Drops: " + earlyDrops + ", Late Drops: " + lateDrops);
    }
}